#include <kern/debug.h>
#include <kern/assert.h>

#include "PrjFSCommon.h"
#include "ActiveProviders.hpp"
#include "Memory.hpp"
#include "Locks.hpp"
#include "KextLog.hpp"
#include "PrjFSProviderUserClient.hpp"

// TODO: turn this into a rwlock
static Mutex s_mutex = {};

// Arbitrary choice, but prevents user space attacker from causing
// allocation of too much wired kernel memory.
static const size_t MaxActiveProviders = 32;

static ActiveProvider s_activeProviders[MaxActiveProviders] = {};

kern_return_t ActiveProviders_Init()
{
    if (Mutex_IsValid(s_mutex))
    {
        return KERN_FAILURE;
    }
    
    s_mutex = Mutex_Alloc();
    if (!Mutex_IsValid(s_mutex))
    {
        return KERN_FAILURE;
    }
    
    return KERN_SUCCESS;
}

kern_return_t ActiveProviders_Cleanup()
{
    if (Mutex_IsValid(s_mutex))
    {
        Mutex_FreeMemory(&s_mutex);        
        return KERN_SUCCESS;
    }
    
    return KERN_FAILURE;
}

ActiveProvider* ActiveProviders_Find(vnode_t vnode)
{
    ActiveProvider* provider = nullptr;
    
    // TODO: only need non-exclusive access here
    Mutex_Acquire(s_mutex);
    {
        // TODO: this is very much a placeholder algorithm
        vnode_get(vnode);
        // Search up the tree until we hit a known virtualization root or THE root of the file system
        while (nullptr == provider && NULLVP != vnode && !vnode_isvroot(vnode))
        {
            for (size_t i = 0; i < MaxActiveProviders; ++i)
            {
                if (vnode == s_activeProviders[i].virtualizationRootVNode)
                {
                    provider = &s_activeProviders[i];
                    break;
                }
            }
            
            vnode_t parent = vnode_getparent(vnode);
            vnode_put(vnode);
            vnode = parent;
        }
        
        if (NULLVP != vnode)
        {
            vnode_put(vnode);
        }
    }
    // TODO: it's currently actually not safe to return the provider outside of the mutex at this stage
    Mutex_Release(s_mutex);
    return provider;
}

static ssize_t FindUnusedIndex_Locked()
{
    for (size_t i = 0; i < MaxActiveProviders; ++i)
    {
        if (nullptr == s_activeProviders[i].userClient)
        {
            return i;
        }
    }
    
    return -1;
}

ActiveProvider* ActiveProvider_RegisterUserClient(PrjFSProviderUserClient* userClient, pid_t clientPID)
{
    ActiveProvider* provider = nullptr;

    Mutex_Acquire(s_mutex);
    {
        ssize_t providerIndex = FindUnusedIndex_Locked();
        if (providerIndex >= 0)
        {
            assert(providerIndex < MaxActiveProviders);
            provider = &s_activeProviders[providerIndex];
            provider->userClient = userClient;
            provider->pid = clientPID;
            assert(NULLVP == provider->virtualizationRootVNode);
        }
    }
    Mutex_Release(s_mutex);

    return provider;
}

// Return values:
// 0:        Virtualization root found and successfully registered
// ENOTDIR:  Selected virtualization root does not resolve to a directory.
// EBUSY:    Already a virtualization root set for this provider.
// ENOENT,…: Error returned by vnode_lookup.
errno_t ActiveProvider_RegisterRoot(ActiveProvider* provider, const char* virtualizationRootPath)
{
    assert(nullptr != virtualizationRootPath);
    assert(provider >= &s_activeProviders[0] && provider < &s_activeProviders[MaxActiveProviders]);
    
    vnode_t virtualizationRootVNode = NULLVP;
    vfs_context_t vfsContext = vfs_context_create(nullptr);
    
    errno_t err = vnode_lookup(virtualizationRootPath, 0 /* flags */, &virtualizationRootVNode, vfsContext);
    if (0 == err && vnode_isdir(virtualizationRootVNode))
    {
        Mutex_Acquire(s_mutex);
        {
            if (NULLVP == provider->virtualizationRootVNode)
            {
                provider->virtualizationRootVNode = virtualizationRootVNode;
                strlcpy(provider->virtualizationRoot, virtualizationRootPath, sizeof(provider->virtualizationRoot));
                virtualizationRootVNode = NULLVP; // prevent vnode_put later; provider should hold vnode reference
            }
            else
            {
                err = EBUSY;
            }
        }
        Mutex_Release(s_mutex);
    }
    else if (0 == err)
    {
        err = ENOTDIR;
    }
    
    if (NULLVP != virtualizationRootVNode)
    {
        vnode_put(virtualizationRootVNode);
    }
    
    vfs_context_rele(vfsContext);
    
    return err;
}

void ActiveProvider_Disconnect(ActiveProvider* provider)
{
    assert(provider >= &s_activeProviders[0] && provider < &s_activeProviders[MaxActiveProviders]);

    Mutex_Acquire(s_mutex);
    {
        assert(nullptr != provider->userClient);
        
        if (NULLVP != provider->virtualizationRootVNode)
        {
            vnode_put(provider->virtualizationRootVNode);
            provider->virtualizationRootVNode = NULLVP;
        }
        
        provider->userClient = nullptr;
        memset(provider->virtualizationRoot, 0, sizeof(provider->virtualizationRoot));
    }
    Mutex_Release(s_mutex);
}

errno_t ActiveProvider_SendMessage(const ActiveProvider* provider, const Message message)
{
    assert(provider >= &s_activeProviders[0] && provider < &s_activeProviders[MaxActiveProviders]);

    // TODO: We shouldn't even be able to have this provider pointer without the lock…
    PrjFSProviderUserClient* userClient = nullptr;
    
    Mutex_Acquire(s_mutex);
    {
        userClient = provider->userClient;
        if (nullptr != userClient)
        {
            userClient->retain();
        }
    }
    Mutex_Release(s_mutex);
    
    if (nullptr != userClient)
    {
        uint32_t messageSize = sizeof(*message.messageHeader) + message.messageHeader->pathSizeBytes;
        uint8_t messageMemory[messageSize];
        memcpy(messageMemory, message.messageHeader, sizeof(*message.messageHeader));
        if (message.messageHeader->pathSizeBytes > 0)
        {
            memcpy(messageMemory + sizeof(*message.messageHeader), message.path, message.messageHeader->pathSizeBytes);
        }
        
        userClient->sendMessage(messageMemory, messageSize);
        userClient->release();
        return 0;
    }
    else
    {
        return EIO;
    }
}
