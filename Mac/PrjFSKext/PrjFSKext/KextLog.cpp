#include <kern/debug.h>
#include <os/log.h>
#include <stdarg.h>

#include "KextLog.hpp"

os_log_t __prjfs_log;

void KextLog_Init()
{
    // TODO: The subsystem and category values are not currently working. Our events get logged, but are missing these fields.
    __prjfs_log = os_log_create("io.gvfs.PrjFS", "Kext");
}

void KextLog_Cleanup()
{
    os_release(__prjfs_log);
    __prjfs_log = nullptr;
}

bool KextLog_RegisterUserClient(PrjFSLogUserClient* userClient)
{
    KextLog_Error("KextLogRegisterUserClient: not yet implemented\n");
    return false;
}
