#ifndef KextLog_h
#define KextLog_h

#include "PrjFSClasses.hpp"
#include <os/log.h>

extern os_log_t __prjfs_log;

void KextLog_Init();
void KextLog_Cleanup();

#define KextLog_Error(format, ...) os_log_error(__prjfs_log, format "\n", ##__VA_ARGS__)
#define KextLog_Info(format, ...) os_log_info(__prjfs_log, format "\n", ##__VA_ARGS__)

bool KextLog_RegisterUserClient(PrjFSLogUserClient* userClient);

#endif /* KextLog_h */
