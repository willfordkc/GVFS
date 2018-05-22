#ifndef Locks_h
#define Locks_h

#include <mach/kern_return.h>

typedef struct __lck_mtx_t__ lck_mtx_t;
typedef struct { lck_mtx_t* p; } Mutex;

kern_return_t Locks_Init();
kern_return_t Locks_Cleanup();

Mutex Mutex_Alloc();
void Mutex_FreeMemory(Mutex* mutex);
bool Mutex_IsValid(Mutex mutex);

void Mutex_Acquire(Mutex mutex);
void Mutex_Release(Mutex mutex);

#endif /* Locks_h */
