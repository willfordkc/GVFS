#ifndef ActiveProviders_h
#define ActiveProviders_h

#include "PrjFSClasses.hpp"
#include "kernel-header-wrappers/vnode.h"

struct ActiveProvider
{
    PrjFSProviderUserClient*    userClient;
    // If non-null, a reference is held (vnode_get/_put), if null, the provider hasn't fully initialised
    vnode_t                     virtualizationRootVNode;
    char                        virtualizationRoot[PrjFSMaxPath];
    int                         pid;
};

kern_return_t ActiveProviders_Init(void);
kern_return_t ActiveProviders_Cleanup(void);

ActiveProvider* ActiveProviders_Find(vnode_t vnode);

ActiveProvider* ActiveProvider_RegisterUserClient(PrjFSProviderUserClient* userClient, pid_t clientPID);
errno_t ActiveProvider_RegisterRoot(ActiveProvider* provider, const char* virtRootPath);
void ActiveProvider_Disconnect(ActiveProvider* provider);

struct Message;
errno_t ActiveProvider_SendMessage(const ActiveProvider* provider, const Message message);

#endif /* ActiveProviders_h */
