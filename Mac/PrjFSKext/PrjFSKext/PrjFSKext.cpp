#include <kern/debug.h>
#include <mach/mach_types.h>

#include "KextLog.hpp"
#include "KauthHandler.hpp"
#include "Locks.hpp"
#include "Memory.hpp"

extern "C" kern_return_t PrjFSKext_Start(kmod_info_t* ki, void* d);
extern "C" kern_return_t PrjFSKext_Stop(kmod_info_t* ki, void* d);

kern_return_t PrjFSKext_Start(kmod_info_t* ki, void* d)
{
    KextLog_Init();
    
    if (Memory_Init())
    {
        goto CleanupAndFail;
    }
    
    if (Locks_Init())
    {
        goto CleanupAndFail;
    }

    if (KauthHandler_Init())
    {
        goto CleanupAndFail;
    }
    
    KextLog_Info("PrjFSKext (Start)");
    return KERN_SUCCESS;
    
CleanupAndFail:
    KextLog_Error("PrjFSKext failed to start");
    
    PrjFSKext_Stop(nullptr, nullptr);
    return KERN_FAILURE;
}

kern_return_t PrjFSKext_Stop(kmod_info_t* ki, void* d)
{
    kern_return_t result = KERN_SUCCESS;
    
    if (KauthHandler_Cleanup())
    {
        result = KERN_FAILURE;
    }
    
    if (Locks_Cleanup())
    {
        result = KERN_FAILURE;
    }
    
    if (Memory_Cleanup())
    {
        result = KERN_FAILURE;
    }
    
    KextLog_Info("PrjFSKext (Stop)");
    KextLog_Cleanup();
    
    return result;
}
