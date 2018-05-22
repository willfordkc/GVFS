#include <kern/debug.h>
#include <kern/locks.h>

#include "PrjFSCommon.h"
#include "Locks.hpp"

static lck_grp_t* s_lockGroup = nullptr;

kern_return_t Locks_Init()
{
    if (nullptr != s_lockGroup)
    {
        return KERN_FAILURE;
    }
    
    s_lockGroup = lck_grp_alloc_init(PrjFSKextBundleId, LCK_GRP_ATTR_NULL);
    if (nullptr == s_lockGroup)
    {
        return KERN_FAILURE;
    }
    
    return KERN_SUCCESS;
}

kern_return_t Locks_Cleanup()
{
    if (nullptr != s_lockGroup)
    {
        lck_grp_free(s_lockGroup);
        s_lockGroup = nullptr;
        
        return KERN_SUCCESS;
    }
    
    return KERN_FAILURE;
}

Mutex Mutex_Alloc()
{
    return (Mutex){ lck_mtx_alloc_init(s_lockGroup, LCK_ATTR_NULL) };
}

void Mutex_FreeMemory(Mutex* mutex)
{
    lck_mtx_free(mutex->p, s_lockGroup);
    mutex->p = nullptr;
}

bool Mutex_IsValid(Mutex mutex)
{
    return mutex.p != nullptr;
}

void Mutex_Acquire(Mutex mutex)
{
    lck_mtx_lock(mutex.p);
}

void Mutex_Release(Mutex mutex)
{
    lck_mtx_unlock(mutex.p);
}
