#ifndef FLUENT_PDF_BRIDGE_H
#define FLUENT_PDF_BRIDGE_H
#include <stddef.h>
#ifdef _WIN32
#define FP_EXPORT __declspec(dllexport)
#else
#define FP_EXPORT __attribute__((visibility("default")))
#endif
/* Implementations run only inside the worker. No pointer crosses the process boundary.
   Identifiers are NUL-terminated ASCII, at most 128 bytes; returned output length
   must not exceed capacity. A nonzero result rejects the whole operation. */
typedef const char *(*fp_identity_fn)(void);
typedef int (*fp_execute_fn)(const unsigned char *, size_t, unsigned char *, size_t, size_t *);
#endif
