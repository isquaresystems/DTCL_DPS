// Minimal C++ runtime support for embedded systems
// This file provides essential C++ runtime functions for STM32

#include <cstdlib>
#include <sys/types.h>

// C++ runtime support - new/delete operators
void* operator new(size_t size) {
    return malloc(size);
}

void* operator new[](size_t size) {
    return malloc(size);
}

void operator delete(void* ptr) {
    free(ptr);
}

void operator delete[](void* ptr) {
    free(ptr);
}

void operator delete(void* ptr, size_t) {
    free(ptr);
}

void operator delete[](void* ptr, size_t) {
    free(ptr);
}

// Pure virtual handler
extern "C" void __cxa_pure_virtual() {
    // Error: pure virtual function called
    while (1);
}

// Guard variables for static initialization
__extension__ typedef int __guard __attribute__((mode(__DI__)));

extern "C" int __cxa_guard_acquire(__guard* g) {
    return !*(char*)(g);
}

extern "C" void __cxa_guard_release(__guard* g) {
    *(char*)g = 1;
}

extern "C" void __cxa_guard_abort(__guard*) {
}

// Atexit support (simplified - does nothing)
extern "C" int __aeabi_atexit(void*, void (*)(void*), void*) {
    return 0;
}
