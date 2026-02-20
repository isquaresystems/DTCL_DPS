// cpp_minimal.cpp - Minimal C++ runtime stubs for embedded
#include <stdint.h>
#include <stdlib.h>

// Static initialization guard functions (single-threaded embedded system)
extern "C" {
    int __cxa_guard_acquire(uint64_t* guard) {
        return !*(char*)guard;
    }
    
    void __cxa_guard_release(uint64_t* guard) {
        *(char*)guard = 1;
    }
    
    void __cxa_guard_abort(uint64_t* guard) {
        // Nothing to do in single-threaded embedded
    }
    
    // Exception personality (stub - exceptions disabled)
    void __gxx_personality_v0() {
        // Never called since exceptions are disabled
    }
    
    // Pure virtual function stub
    void __cxa_pure_virtual() {
        while (1) { } // Halt if pure virtual called
    }
    
    // Exception cleanup (stub - exceptions disabled)
    void __cxa_end_cleanup() {
        // Never called since exceptions are disabled
    }
}

// Memory allocation operators (using malloc/free for now)
void* operator new(size_t size) {
    return malloc(size);
}

void* operator new[](size_t size) {
    return malloc(size);
}

void operator delete(void* ptr) noexcept {
    free(ptr);
}

void operator delete[](void* ptr) noexcept {
    free(ptr);
}

void operator delete(void* ptr, size_t) noexcept {
    free(ptr);
}

void operator delete[](void* ptr, size_t) noexcept {
    free(ptr);
}

// STL exception throwing functions (stub - exceptions disabled)
namespace std {
    void __throw_bad_alloc() {
        while (1) { } // Halt on allocation failure
    }
    
    void __throw_length_error(const char*) {
        while (1) { } // Halt on length error
    }
    
    void __throw_out_of_range(const char*) {
        while (1) { } // Halt on out of range
    }
    
    void __throw_runtime_error(const char*) {
        while (1) { } // Halt on runtime error
    }
}

// STL red-black tree functions (minimal implementation stubs)
struct _Rb_tree_node_base {
    int color;
    struct _Rb_tree_node_base* parent;
    struct _Rb_tree_node_base* left;
    struct _Rb_tree_node_base* right;
};

namespace std {
    void _Rb_tree_insert_and_rebalance(bool, _Rb_tree_node_base*, 
                                       _Rb_tree_node_base*, 
                                       _Rb_tree_node_base&) {
        // Minimal stub - just link nodes without rebalancing
    }
    
    _Rb_tree_node_base* _Rb_tree_increment(_Rb_tree_node_base* node) {
        if (!node) return nullptr;
        if (node->right) {
            node = node->right;
            while (node->left) node = node->left;
        } else {
            _Rb_tree_node_base* parent = node->parent;
            while (parent && node == parent->right) {
                node = parent;
                parent = parent->parent;
            }
            if (node->right != parent) node = parent;
        }
        return node;
    }
    
    _Rb_tree_node_base* _Rb_tree_decrement(_Rb_tree_node_base* node) {
        if (!node) return nullptr;
        if (node->left) {
            node = node->left;
            while (node->right) node = node->right;
        } else {
            _Rb_tree_node_base* parent = node->parent;
            while (parent && node == parent->left) {
                node = parent;
                parent = parent->parent;
            }
            node = parent;
        }
        return node;
    }
}

// RTTI support stubs
namespace __cxxabiv1 {
    class __class_type_info {
    public:
        virtual ~__class_type_info() = default;
    };
    
    class __si_class_type_info : public __class_type_info {
    public:
        virtual ~__si_class_type_info() = default;
    };
}