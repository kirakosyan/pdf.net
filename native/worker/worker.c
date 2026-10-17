/* Original offline worker host. The bridge library is loaded only after the
   platform sandbox and resource boundaries are installed. */
#define _GNU_SOURCE
#include "bridge.h"
#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>
#include <string.h>
#include <errno.h>
#include <limits.h>
#ifdef _WIN32
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <userenv.h>
#include <aclapi.h>
#include <sddl.h>
#include <netfw.h>
#include <io.h>
#include <fcntl.h>
#pragma comment(lib, "userenv.lib")
#pragma comment(lib, "advapi32.lib")
#pragma comment(lib, "ole32.lib")
#pragma comment(lib, "oleaut32.lib")
#pragma comment(lib, "uuid.lib")
#else
#include <unistd.h>
#include <fcntl.h>
#include <signal.h>
#include <sys/resource.h>
#include <dlfcn.h>
#endif
#ifdef __linux__
#include <stddef.h>
#include <sys/prctl.h>
#include <sys/syscall.h>
#include <linux/landlock.h>
#include <linux/seccomp.h>
#include <linux/filter.h>
#include <linux/audit.h>
#include <linux/sched.h>
#include <linux/capability.h>
#endif
#ifdef __APPLE__
#include <sandbox.h>
#include <sys/event.h>
#include <pthread.h>
#include <mach/mach.h>
#endif

enum { FP_OK=0, FP_ABI=1, FP_VERSION=2, FP_DEPENDENCY=3, FP_EXPORT_MISSING=4,
       FP_SANDBOX=5, FP_INPUT=6, FP_ENGINE=7, FP_MEMORY=8, FP_PROTOCOL=9 };
static unsigned char nonce[16];
static unsigned sandbox_step;
static uint32_t get32(const unsigned char *p) {
    return (uint32_t)p[0] | (uint32_t)p[1]<<8 | (uint32_t)p[2]<<16 | (uint32_t)p[3]<<24;
}
static void put32(unsigned char *p, uint32_t n) {
    unsigned i; for(i=0;i<4;i++) p[i]=(unsigned char)(n>>(8*i));
}
static int respond(unsigned status, const unsigned char *output, uint32_t length) {
    unsigned char header[32]={'F','P','N','W','0','0','0','1'};
    unsigned char failure[8];
    if(status==FP_SANDBOX && length==0) {
        put32(failure,sandbox_step);
#ifdef _WIN32
        put32(failure+4,GetLastError());
#else
        put32(failure+4,(uint32_t)errno);
#endif
        output=failure; length=8;
    }
    memcpy(header+8,nonce,16); put32(header+24,status); put32(header+28,length);
    if(fwrite(header,1,sizeof(header),stdout)!=sizeof(header)) return 90;
    if(length && fwrite(output,1,length,stdout)!=length) return 90;
    return fflush(stdout)==0 ? 0 : 90;
}
static int parse_nonce(const char *s) {
    unsigned i; if(strlen(s)!=32) return 0;
    for(i=0;i<16;i++) {
        unsigned a,b; char x=s[2*i],y=s[2*i+1];
        if(x>='0'&&x<='9') a=(unsigned)(x-'0'); else if(x>='a'&&x<='f') a=(unsigned)(x-'a'+10); else return 0;
        if(y>='0'&&y<='9') b=(unsigned)(y-'0'); else if(y>='a'&&y<='f') b=(unsigned)(y-'a'+10); else return 0;
        nonce[i]=(unsigned char)((a<<4)|b);
    } return 1;
}
static int parse_limit(const char *s, uint64_t *value) {
    char *end; unsigned long long n;
    if(!*s||*s=='-'||*s=='+') return 0;
    errno=0; n=strtoull(s,&end,10); if(errno||*end) return 0;
    *value=(uint64_t)n; return 1;
}
static int same_identity(const char *actual, const char *expected) {
    size_t n; if(!actual) return 0;
    for(n=0;n<=128;n++) { if(actual[n]!=expected[n]) return 0; if(actual[n]==0) return 1; }
    return 0;
}

#ifdef __linux__
static int allow_read_path(int rules, const char *path, int directory) {
    int fd=open(path,O_PATH|O_CLOEXEC); struct landlock_path_beneath_attr rule;
    if(fd<0) return errno==ENOENT ? 1 : 0;
    memset(&rule,0,sizeof(rule)); rule.parent_fd=fd;
    rule.allowed_access=LANDLOCK_ACCESS_FS_READ_FILE | (directory ? LANDLOCK_ACCESS_FS_READ_DIR|LANDLOCK_ACCESS_FS_EXECUTE : 0);
    if(syscall(__NR_landlock_add_rule,rules,LANDLOCK_RULE_PATH_BENEATH,&rule,0)<0) { close(fd); return 0; }
    close(fd); return 1;
}
static int linux_sandbox(const char *root, int threaded) {
    int abi=(int)syscall(__NR_landlock_create_ruleset,NULL,0,LANDLOCK_CREATE_RULESET_VERSION);
    int rules; struct landlock_ruleset_attr policy;
    struct sock_filter filter[]={
        BPF_STMT(BPF_LD|BPF_W|BPF_ABS,offsetof(struct seccomp_data,arch)),
#if defined(__x86_64__)
        BPF_JUMP(BPF_JMP|BPF_JEQ|BPF_K,AUDIT_ARCH_X86_64,1,0),
#elif defined(__aarch64__)
        BPF_JUMP(BPF_JMP|BPF_JEQ|BPF_K,AUDIT_ARCH_AARCH64,1,0),
#else
#error Unsupported native worker architecture
#endif
        BPF_STMT(BPF_RET|BPF_K,SECCOMP_RET_KILL_PROCESS),
        BPF_STMT(BPF_LD|BPF_W|BPF_ABS,offsetof(struct seccomp_data,nr)),
        /* Thread creation is opt-in. Fork/vfork and clone namespace/process
           flags remain denied; every new thread inherits Landlock/seccomp. */
#ifdef __NR_clone3
        BPF_JUMP(BPF_JMP|BPF_JEQ|BPF_K,__NR_clone3,0,1),
        BPF_STMT(BPF_RET|BPF_K,SECCOMP_RET_ERRNO|ENOSYS),
#endif
        BPF_JUMP(BPF_JMP|BPF_JEQ|BPF_K,threaded?(unsigned)__NR_clone:UINT_MAX,0,12),
        BPF_STMT(BPF_LD|BPF_W|BPF_ABS,offsetof(struct seccomp_data,args[0])+4),
        BPF_JUMP(BPF_JMP|BPF_JEQ|BPF_K,0,1,0),
        BPF_STMT(BPF_RET|BPF_K,SECCOMP_RET_ERRNO|EPERM),
        BPF_STMT(BPF_LD|BPF_W|BPF_ABS,offsetof(struct seccomp_data,args[0])),
        BPF_STMT(BPF_ALU|BPF_AND|BPF_K,CLONE_VM|CLONE_THREAD|CLONE_SIGHAND),
        BPF_JUMP(BPF_JMP|BPF_JEQ|BPF_K,CLONE_VM|CLONE_THREAD|CLONE_SIGHAND,1,0),
        BPF_STMT(BPF_RET|BPF_K,SECCOMP_RET_ERRNO|EPERM),
        BPF_STMT(BPF_LD|BPF_W|BPF_ABS,offsetof(struct seccomp_data,args[0])),
        BPF_STMT(BPF_ALU|BPF_AND|BPF_K,~(CLONE_VM|CLONE_FS|CLONE_FILES|CLONE_SIGHAND|CLONE_THREAD|CLONE_SYSVSEM|CLONE_SETTLS|CLONE_PARENT_SETTID|CLONE_CHILD_CLEARTID|CLONE_CHILD_SETTID|CLONE_DETACHED)),
        BPF_JUMP(BPF_JMP|BPF_JEQ|BPF_K,0,1,0),
        BPF_STMT(BPF_RET|BPF_K,SECCOMP_RET_ERRNO|EPERM),
        BPF_STMT(BPF_RET|BPF_K,SECCOMP_RET_ALLOW),
        BPF_JUMP(BPF_JMP|BPF_JEQ|BPF_K,__NR_tgkill,0,4),
        BPF_STMT(BPF_LD|BPF_W|BPF_ABS,offsetof(struct seccomp_data,args[0])),
        BPF_JUMP(BPF_JMP|BPF_JEQ|BPF_K,(unsigned)getpid(),1,0),
        BPF_STMT(BPF_RET|BPF_K,SECCOMP_RET_ERRNO|EPERM),
        BPF_STMT(BPF_RET|BPF_K,SECCOMP_RET_ALLOW),
        /* Permit only the single-process bridge's memory, read-only loader,
           clock and inherited-pipe operations. Unknown/new syscalls fail closed. */
        BPF_JUMP(BPF_JMP|BPF_JEQ|BPF_K,__NR_prlimit64,0,4),
        BPF_STMT(BPF_LD|BPF_W|BPF_ABS,offsetof(struct seccomp_data,args[0])),
        BPF_JUMP(BPF_JMP|BPF_JEQ|BPF_K,0,1,0),
        BPF_STMT(BPF_RET|BPF_K,SECCOMP_RET_ERRNO|EPERM),
        BPF_STMT(BPF_RET|BPF_K,SECCOMP_RET_ALLOW),
#define ALLOW_CALL(number) BPF_JUMP(BPF_JMP|BPF_JEQ|BPF_K,(number),0,1), BPF_STMT(BPF_RET|BPF_K,SECCOMP_RET_ALLOW)
        ALLOW_CALL(__NR_read), ALLOW_CALL(__NR_write), ALLOW_CALL(__NR_readv), ALLOW_CALL(__NR_writev),
        ALLOW_CALL(__NR_close), ALLOW_CALL(__NR_fstat), ALLOW_CALL(__NR_newfstatat), ALLOW_CALL(__NR_statx),
        ALLOW_CALL(__NR_openat), ALLOW_CALL(__NR_faccessat), ALLOW_CALL(__NR_lseek),
        ALLOW_CALL(__NR_mmap), ALLOW_CALL(__NR_mprotect), ALLOW_CALL(__NR_munmap), ALLOW_CALL(__NR_mremap),
        ALLOW_CALL(__NR_madvise), ALLOW_CALL(__NR_brk), ALLOW_CALL(__NR_msync),
        ALLOW_CALL(__NR_rt_sigaction), ALLOW_CALL(__NR_rt_sigprocmask), ALLOW_CALL(__NR_rt_sigreturn), ALLOW_CALL(__NR_sigaltstack),
        ALLOW_CALL(__NR_clock_gettime), ALLOW_CALL(__NR_clock_getres), ALLOW_CALL(__NR_gettimeofday),
        ALLOW_CALL(__NR_nanosleep), ALLOW_CALL(__NR_clock_nanosleep), ALLOW_CALL(__NR_sched_yield), ALLOW_CALL(__NR_sched_getaffinity),
        ALLOW_CALL(__NR_getpid), ALLOW_CALL(__NR_gettid), ALLOW_CALL(__NR_getppid),
        ALLOW_CALL(__NR_getuid), ALLOW_CALL(__NR_geteuid), ALLOW_CALL(__NR_getgid), ALLOW_CALL(__NR_getegid),
        ALLOW_CALL(__NR_uname), ALLOW_CALL(__NR_sysinfo), ALLOW_CALL(__NR_getcwd), ALLOW_CALL(__NR_getdents64),
        ALLOW_CALL(__NR_readlinkat), ALLOW_CALL(__NR_fcntl), ALLOW_CALL(__NR_dup), ALLOW_CALL(__NR_dup3),
        ALLOW_CALL(__NR_getrandom), ALLOW_CALL(__NR_futex), ALLOW_CALL(__NR_set_robust_list),
        ALLOW_CALL(__NR_rt_sigpending), ALLOW_CALL(__NR_rt_sigtimedwait), ALLOW_CALL(__NR_rt_sigsuspend),
        ALLOW_CALL(__NR_eventfd2), ALLOW_CALL(__NR_pipe2), ALLOW_CALL(__NR_ppoll), ALLOW_CALL(__NR_pselect6),
        ALLOW_CALL(__NR_epoll_create1), ALLOW_CALL(__NR_epoll_ctl), ALLOW_CALL(__NR_epoll_pwait), ALLOW_CALL(__NR_mincore),
        ALLOW_CALL(__NR_getrlimit), ALLOW_CALL(__NR_setrlimit), ALLOW_CALL(__NR_getrusage),
        ALLOW_CALL(__NR_exit), ALLOW_CALL(__NR_exit_group),
#ifdef __NR_open
        ALLOW_CALL(__NR_open), ALLOW_CALL(__NR_access), ALLOW_CALL(__NR_stat), ALLOW_CALL(__NR_lstat),
        ALLOW_CALL(__NR_readlink), ALLOW_CALL(__NR_dup2), ALLOW_CALL(__NR_time), ALLOW_CALL(__NR_arch_prctl),
#endif
#ifdef __NR_rseq
        ALLOW_CALL(__NR_rseq),
#endif
#ifdef __NR_faccessat2
        ALLOW_CALL(__NR_faccessat2),
#endif
#ifdef __NR_futex_waitv
        ALLOW_CALL(__NR_futex_waitv),
#endif
#ifdef __NR_membarrier
        ALLOW_CALL(__NR_membarrier),
#endif
#undef ALLOW_CALL
        BPF_STMT(BPF_RET|BPF_K,SECCOMP_RET_ERRNO|EPERM)
    };
    struct sock_fprog program={(unsigned short)(sizeof(filter)/sizeof(filter[0])),filter};
    if(abi<1 || prctl(PR_SET_NO_NEW_PRIVS,1,0,0,0)<0) return 0;
    memset(&policy,0,sizeof(policy));
    policy.handled_access_fs=(UINT64_C(1)<<13)-1;
    if(abi>=2) policy.handled_access_fs|=UINT64_C(1)<<13;
    if(abi>=3) policy.handled_access_fs|=UINT64_C(1)<<14;
    rules=(int)syscall(__NR_landlock_create_ruleset,&policy,sizeof(policy),0);
    if(rules<0) return 0;
    if(!allow_read_path(rules,root,1)||!allow_read_path(rules,"/lib",1)||!allow_read_path(rules,"/lib64",1)||
       !allow_read_path(rules,"/usr/lib",1)||!allow_read_path(rules,"/usr/lib64",1)||
       !allow_read_path(rules,"/etc/ld.so.cache",0)||!allow_read_path(rules,"/dev/null",0)||
       syscall(__NR_landlock_restrict_self,rules,0)<0) { close(rules); return 0; }
    close(rules);
    return prctl(PR_SET_SECCOMP,SECCOMP_MODE_FILTER,&program)==0;
}
#endif

#ifdef __APPLE__
static void *watch_parent(void *argument) {
    int queue=(int)(intptr_t)argument; struct kevent event;
    while(kevent(queue,NULL,0,&event,1,NULL)<0 && errno==EINTR) { }
    _exit(94);
    return NULL;
}
static int parent_watch(uint64_t parent) {
    int queue; struct kevent change; pthread_t thread; pthread_attr_t attributes;
    if((uint64_t)getppid()!=parent) return 0;
    queue=kqueue(); if(queue<0) return 0;
    EV_SET(&change,(uintptr_t)parent,EVFILT_PROC,EV_ADD|EV_ONESHOT,NOTE_EXIT,0,NULL);
    if(kevent(queue,&change,1,NULL,0,NULL)<0) { close(queue); return 0; }
    if(pthread_attr_init(&attributes)!=0) { close(queue); return 0; }
    if(pthread_attr_setstacksize(&attributes,65536)!=0 || pthread_create(&thread,&attributes,watch_parent,(void *)(intptr_t)queue)!=0) {
        pthread_attr_destroy(&attributes); close(queue); return 0;
    }
    pthread_attr_destroy(&attributes); pthread_detach(thread);
    return (uint64_t)getppid()==parent;
}
static int mac_sandbox(const char *root) {
    char escaped[8192],profile[10000],*error=NULL; size_t i,j=0;
    for(i=0;root[i];i++) { if(j+3>=sizeof(escaped)) return 0; if(root[i]=='"'||root[i]=='\\') escaped[j++]='\\'; escaped[j++]=root[i]; }
    escaped[j]=0;
    if(snprintf(profile,sizeof(profile),"(version 1)(deny default)(allow process-info* (target self))(allow sysctl-read)"
        "(allow file-map-executable (subpath \"%s\") (subpath \"/usr/lib\") (subpath \"/System\"))"
        "(allow file-read* (subpath \"%s\") (subpath \"/usr/lib\") (subpath \"/System\") (subpath \"/private/var/db/dyld\"))",escaped,escaped)<0) return 0;
    /* Existing stdin/stdout pipes are the sole transport; no network or writes are granted. */
    if(sandbox_init(profile,0,&error)!=0) { if(error) sandbox_free_error(error); return 0; }
    return 1;
}
#endif

#ifndef _WIN32
static void fatal_signal(int number) { _exit(128+number); }
static int install_fault_boundary(void) {
    stack_t stack; struct sigaction action; int signals[]={SIGSEGV,SIGBUS,SIGILL,SIGFPE,SIGABRT}; unsigned i;
    memset(&stack,0,sizeof(stack)); stack.ss_sp=malloc(65536); stack.ss_size=65536;
    if(!stack.ss_sp || sigaltstack(&stack,NULL)!=0) return 0;
    memset(&action,0,sizeof(action)); action.sa_handler=fatal_signal; action.sa_flags=SA_ONSTACK;
    sigemptyset(&action.sa_mask);
    for(i=0;i<sizeof(signals)/sizeof(signals[0]);i++) if(sigaction(signals[i],&action,NULL)!=0) return 0;
    return 1;
}
static int unix_limits(const char *root, uint64_t memory, uint64_t milliseconds, uint64_t parent, int threaded) {
    struct rlimit limit; char canonical[PATH_MAX];
    sandbox_step=1;
    if(!realpath(root,canonical)) return 0;
    if(!install_fault_boundary()) return 0;
    sandbox_step=2;
    limit.rlim_cur=limit.rlim_max=0; if(setrlimit(RLIMIT_CORE,&limit)!=0) return 0;
    sandbox_step=3;
    limit.rlim_cur=limit.rlim_max=(rlim_t)((milliseconds+999)/1000+1); if(setrlimit(RLIMIT_CPU,&limit)!=0) return 0;
    sandbox_step=4;
    limit.rlim_cur=limit.rlim_max=(rlim_t)memory;
#ifdef __linux__
    if(threaded) {
        struct __user_cap_header_struct header={_LINUX_CAPABILITY_VERSION_3,0};
        struct __user_cap_data_struct capabilities[2];struct rlimit tasks;
        sandbox_step=18;
        if(getuid()==0 || geteuid()==0) {errno=EPERM;return 0;}
        memset(capabilities,0,sizeof(capabilities));
        if(syscall(__NR_capget,&header,capabilities)<0) return 0;
        if(capabilities[0].effective||capabilities[0].permitted||capabilities[0].inheritable||
           capabilities[1].effective||capabilities[1].permitted||capabilities[1].inheritable) {errno=EPERM;return 0;}
        if(getrlimit(RLIMIT_NPROC,&tasks)!=0) return 0;
        if(tasks.rlim_cur>1024)tasks.rlim_cur=1024;
        if(tasks.rlim_max>1024)tasks.rlim_max=1024;
        if(setrlimit(RLIMIT_NPROC,&tasks)!=0) return 0;
    }
    if(setrlimit(RLIMIT_AS,&limit)!=0 || setrlimit(RLIMIT_DATA,&limit)!=0) return 0;
    if(prctl(PR_SET_PDEATHSIG,SIGKILL)!=0 || (uint64_t)getppid()!=parent) return 0;
#else
    (void)threaded;
    sandbox_step=5;
    if(!parent_watch(parent)) return 0;
#endif
    sandbox_step=6;
    limit.rlim_cur=limit.rlim_max=0; if(setrlimit(RLIMIT_FSIZE,&limit)!=0) return 0;
    sandbox_step=7;
    limit.rlim_cur=limit.rlim_max=64; if(setrlimit(RLIMIT_NOFILE,&limit)!=0) return 0;
    sandbox_step=9;
#ifdef __linux__
    return linux_sandbox(canonical,threaded);
#else
    if(!mac_sandbox(canonical)) return 0;
    sandbox_step=4;
    {
        struct task_basic_info_64 information;
        mach_msg_type_number_t count=TASK_BASIC_INFO_64_COUNT;
        if(task_info(mach_task_self(),TASK_BASIC_INFO_64,(task_info_t)&information,&count)!=KERN_SUCCESS ||
            information.virtual_size>UINT64_MAX-memory) return 0;
        /* Install the VM-growth budget after trusted policy compilation and
           before the first untrusted library load. Absolute RSS is supervised. */
        limit.rlim_cur=limit.rlim_max=(rlim_t)(information.virtual_size+memory);
        if(threaded) fprintf(stderr,"worker VM baseline=%llu growth-limit=%llu absolute-limit=%llu\n",
            (unsigned long long)information.virtual_size,(unsigned long long)memory,(unsigned long long)limit.rlim_cur);
    }
    return setrlimit(RLIMIT_DATA,&limit)==0;
#endif
}
#endif

#ifdef _WIN32
static int network_isolation_available(void) {
    INetFwPolicy2 *policy=NULL; HRESULT initialized,result; int ok=1,i;
    NET_FW_PROFILE_TYPE2 profiles[]={NET_FW_PROFILE2_DOMAIN,NET_FW_PROFILE2_PRIVATE,NET_FW_PROFILE2_PUBLIC};
    initialized=CoInitializeEx(NULL,COINIT_MULTITHREADED);
    if(FAILED(initialized)) return 0;
    result=CoCreateInstance(&CLSID_NetFwPolicy2,NULL,CLSCTX_INPROC_SERVER,&IID_INetFwPolicy2,(void **)&policy);
    if(FAILED(result)) ok=0;
    else {
        for(i=0;i<3;i++) {
            VARIANT_BOOL enabled=VARIANT_FALSE;
            if(FAILED(policy->lpVtbl->get_FirewallEnabled(policy,profiles[i],&enabled)) || enabled!=VARIANT_TRUE) ok=0;
        }
        policy->lpVtbl->Release(policy);
    }
    CoUninitialize();
    if(!ok) SetLastError(ERROR_SERVICE_DISABLED);
    return ok;
}
static int grant_stage(PSID sid, const wchar_t *root) {
    PACL old_acl=NULL,new_acl=NULL; PSECURITY_DESCRIPTOR descriptor=NULL;
    EXPLICIT_ACCESSW entry; DWORD status;
    status=GetNamedSecurityInfoW((LPWSTR)root,SE_FILE_OBJECT,DACL_SECURITY_INFORMATION,NULL,NULL,&old_acl,NULL,&descriptor);
    if(status!=ERROR_SUCCESS) return 0;
    memset(&entry,0,sizeof(entry)); entry.grfAccessPermissions=FILE_GENERIC_READ|FILE_GENERIC_EXECUTE;
    entry.grfAccessMode=GRANT_ACCESS; entry.grfInheritance=SUB_CONTAINERS_AND_OBJECTS_INHERIT;
    entry.Trustee.TrusteeForm=TRUSTEE_IS_SID; entry.Trustee.TrusteeType=TRUSTEE_IS_WELL_KNOWN_GROUP; entry.Trustee.ptstrName=(LPWSTR)sid;
    status=SetEntriesInAclW(1,&entry,old_acl,&new_acl);
    if(status==ERROR_SUCCESS) status=SetNamedSecurityInfoW((LPWSTR)root,SE_FILE_OBJECT,DACL_SECURITY_INFORMATION,NULL,NULL,new_acl,NULL);
    if(new_acl) LocalFree(new_acl); LocalFree(descriptor); return status==ERROR_SUCCESS;
}
static int windows_child(int argc, char **argv, uint64_t memory, uint64_t milliseconds, uint64_t parent_id) {
    wchar_t profile[100],root[4096],exe[4096],command[24000]; size_t used; int i,result=91;
    PSID sid=NULL; HANDLE job=NULL,parent_process=NULL; JOBOBJECT_EXTENDED_LIMIT_INFORMATION limits;
    STARTUPINFOEXW startup; PROCESS_INFORMATION child; SECURITY_CAPABILITIES capabilities;
    SIZE_T attribute_bytes=0; LPPROC_THREAD_ATTRIBUTE_LIST attributes=NULL; DWORD code,failure_code=0; int attributes_ready=0;
    HANDLE inherited[3]={GetStdHandle(STD_INPUT_HANDLE),GetStdHandle(STD_OUTPUT_HANDLE),GetStdHandle(STD_ERROR_HANDLE)};
    sandbox_step=10;
    if(!network_isolation_available()) goto done;
    swprintf(profile,100,L"Fluent.Pdf.worker.%lu.%hs",GetCurrentProcessId(),argv[9]);
    if(MultiByteToWideChar(CP_UTF8,MB_ERR_INVALID_CHARS,argv[1],-1,root,4096)==0) return respond(FP_SANDBOX,NULL,0);
    if(FAILED(CreateAppContainerProfile(profile,L"Fluent.Pdf isolated worker",L"Single offline job",NULL,0,&sid))) goto done;
    parent_process=OpenProcess(SYNCHRONIZE,FALSE,(DWORD)parent_id); if(!parent_process) goto done;
    sandbox_step=11;
    if(!grant_stage(sid,root)) goto done;
    sandbox_step=12;
    job=CreateJobObjectW(NULL,NULL); if(!job) goto done;
    memset(&limits,0,sizeof(limits)); limits.BasicLimitInformation.LimitFlags=JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE|
        JOB_OBJECT_LIMIT_ACTIVE_PROCESS|JOB_OBJECT_LIMIT_PROCESS_MEMORY|JOB_OBJECT_LIMIT_JOB_MEMORY|JOB_OBJECT_LIMIT_PROCESS_TIME;
    limits.BasicLimitInformation.ActiveProcessLimit=1; limits.ProcessMemoryLimit=(SIZE_T)memory; limits.JobMemoryLimit=(SIZE_T)memory;
    limits.BasicLimitInformation.PerProcessUserTimeLimit.QuadPart=(LONGLONG)(milliseconds*10000+10000000);
    sandbox_step=13;
    if(!SetInformationJobObject(job,JobObjectExtendedLimitInformation,&limits,sizeof(limits))) goto done;
    if(!GetModuleFileNameW(NULL,exe,4096)) goto done;
    used=(size_t)swprintf(command,24000,L"\"%ls\" --inside",exe);
    for(i=1;i<argc;i++) {
        wchar_t argument[4096]; size_t j;
        if(!MultiByteToWideChar(CP_UTF8,MB_ERR_INVALID_CHARS,argv[i],-1,argument,4096)) goto done;
        if(used+3>=24000) goto done; command[used++]=L' '; command[used++]=L'"';
        for(j=0;argument[j];j++) { if(used+3>=24000||argument[j]==L'"') goto done; command[used++]=argument[j]; }
        /* A normalized root never ends with a separator; the remaining arguments are portable identifiers/numbers. */
        command[used++]=L'"'; command[used]=0;
    }
    memset(&startup,0,sizeof(startup)); memset(&child,0,sizeof(child)); memset(&capabilities,0,sizeof(capabilities));
    InitializeProcThreadAttributeList(NULL,3,0,&attribute_bytes);
    sandbox_step=14;
    attributes=(LPPROC_THREAD_ATTRIBUTE_LIST)HeapAlloc(GetProcessHeap(),0,attribute_bytes); if(!attributes) goto done;
    if(!InitializeProcThreadAttributeList(attributes,3,0,&attribute_bytes)) goto done;
    attributes_ready=1;
    capabilities.AppContainerSid=sid;
    sandbox_step=15;
    if(!UpdateProcThreadAttribute(attributes,0,PROC_THREAD_ATTRIBUTE_SECURITY_CAPABILITIES,&capabilities,sizeof(capabilities),NULL,NULL)) goto done;
    if(!UpdateProcThreadAttribute(attributes,0,PROC_THREAD_ATTRIBUTE_JOB_LIST,&job,sizeof(job),NULL,NULL)) goto done;
    for(i=0;i<3;i++) if(!SetHandleInformation(inherited[i],HANDLE_FLAG_INHERIT,HANDLE_FLAG_INHERIT)) goto done;
    if(!UpdateProcThreadAttribute(attributes,0,PROC_THREAD_ATTRIBUTE_HANDLE_LIST,inherited,sizeof(inherited),NULL,NULL)) goto done;
    startup.StartupInfo.cb=sizeof(startup); startup.StartupInfo.dwFlags=STARTF_USESTDHANDLES;
    startup.StartupInfo.hStdInput=GetStdHandle(STD_INPUT_HANDLE); startup.StartupInfo.hStdOutput=GetStdHandle(STD_OUTPUT_HANDLE);
    startup.StartupInfo.hStdError=GetStdHandle(STD_ERROR_HANDLE); startup.lpAttributeList=attributes;
    sandbox_step=16;
    if(!CreateProcessW(exe,command,NULL,NULL,TRUE,EXTENDED_STARTUPINFO_PRESENT|CREATE_SUSPENDED|CREATE_NO_WINDOW,NULL,root,&startup.StartupInfo,&child)) goto done;
    sandbox_step=17;
    if(ResumeThread(child.hThread)==(DWORD)-1) { TerminateJobObject(job,91); }
    {
        HANDLE observed[2]={child.hProcess,parent_process};
        DWORD wait=WaitForMultipleObjects(2,observed,FALSE,INFINITE);
        if(wait!=WAIT_OBJECT_0) { TerminateJobObject(job,91); WaitForSingleObject(child.hProcess,4000); }
    }
    if(GetExitCodeProcess(child.hProcess,&code)) result=(int)code;
    CloseHandle(child.hThread); CloseHandle(child.hProcess);
done:
    if(result==91) failure_code=GetLastError();
    if(job) CloseHandle(job);
    if(parent_process) CloseHandle(parent_process);
    if(attributes) { if(attributes_ready) DeleteProcThreadAttributeList(attributes); HeapFree(GetProcessHeap(),0,attributes); }
    if(sid) { FreeSid(sid); DeleteAppContainerProfile(profile); }
    if(result==91) { SetLastError(failure_code); return respond(FP_SANDBOX,NULL,0); }
    return result;
}
#endif

static int worker_main(int argc, char **argv) {
    int offset=1,status; char **p; uint64_t max_input,max_output,memory,milliseconds,parent_id,threaded;
    unsigned char size_bytes[4],*input=NULL,*output=NULL; uint32_t input_size; size_t output_size=0;
    fp_identity_fn abi_fn,version_fn; fp_execute_fn execute_fn;
#ifdef _WIN32
    HMODULE library; wchar_t library_path[8192],root_path[4096],file_name[256];
    _setmode(_fileno(stdin),_O_BINARY); _setmode(_fileno(stdout),_O_BINARY);
    SetErrorMode(SEM_FAILCRITICALERRORS|SEM_NOGPFAULTERRORBOX|SEM_NOOPENFILEERRORBOX);
    if(argc>1&&strcmp(argv[1],"--inside")==0) offset=2;
#else
    void *library; char library_path[8192];
#endif
    if(argc-offset!=11) return 92;
    p=argv+offset;
    if(!parse_nonce(p[8])||!parse_limit(p[4],&max_input)||!parse_limit(p[5],&max_output)||
       !parse_limit(p[6],&memory)||!parse_limit(p[7],&milliseconds)||!parse_limit(p[9],&parent_id)||!parse_limit(p[10],&threaded)||threaded>1||parent_id==0||parent_id>UINT32_MAX||max_input>INT_MAX||max_output>INT_MAX||
       memory<16777216||memory>UINT64_C(1099511627776)||milliseconds==0||milliseconds>86400000) return 92;
    if(strlen(p[2])>128||strlen(p[3])>128||strchr(p[1],'/')||strchr(p[1],'\\')) return 92;
#ifdef _WIN32
    if(sizeof(SIZE_T)==4 && memory>UINT32_MAX) return respond(FP_MEMORY,NULL,0);
    if(offset==1) return windows_child(argc,argv,memory,milliseconds,parent_id);
    if(!MultiByteToWideChar(CP_UTF8,MB_ERR_INVALID_CHARS,p[0],-1,root_path,4096)||
       !MultiByteToWideChar(CP_UTF8,MB_ERR_INVALID_CHARS,p[1],-1,file_name,256)) return respond(FP_PROTOCOL,NULL,0);
    if(swprintf(library_path,8192,L"%ls\\%ls",root_path,file_name)<0) return respond(FP_PROTOCOL,NULL,0);
    SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_SYSTEM32);
    library=LoadLibraryExW(library_path,NULL,LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR|LOAD_LIBRARY_SEARCH_SYSTEM32);
    if(!library) {
        unsigned char loader_error[4]; put32(loader_error,GetLastError());
        return respond(FP_DEPENDENCY,loader_error,4);
    }
    {
        FARPROC symbol;
        _Static_assert(sizeof(symbol)==sizeof(abi_fn),"Windows bridge function pointer size differs");
        symbol=GetProcAddress(library,"fluent_pdf_bridge_abi_v1"); memcpy(&abi_fn,&symbol,sizeof(abi_fn));
        symbol=GetProcAddress(library,"fluent_pdf_bridge_version_v1"); memcpy(&version_fn,&symbol,sizeof(version_fn));
        symbol=GetProcAddress(library,"fluent_pdf_bridge_execute_v1"); memcpy(&execute_fn,&symbol,sizeof(execute_fn));
    }
#else
    {
        char candidate[8192];
        int length=snprintf(candidate,sizeof(candidate),"%s/%s",p[0],p[1]);
        if(length<0 || (size_t)length>=sizeof(candidate) || !realpath(candidate,library_path)) return respond(FP_DEPENDENCY,NULL,0);
    }
    if(!unix_limits(p[0],memory,milliseconds,parent_id,(int)threaded)) return respond(FP_SANDBOX,NULL,0);
    library=dlopen(library_path,RTLD_LOCAL|RTLD_NOW);
    if(!library) {
        const char *detail=dlerror(); size_t length=0;
        if(detail) while(length<2048 && detail[length]) length++;
        return respond(FP_DEPENDENCY,(const unsigned char *)detail,(uint32_t)length);
    }
    *(void **)(&abi_fn)=dlsym(library,"fluent_pdf_bridge_abi_v1");
    *(void **)(&version_fn)=dlsym(library,"fluent_pdf_bridge_version_v1");
    *(void **)(&execute_fn)=dlsym(library,"fluent_pdf_bridge_execute_v1");
#endif
    if(!abi_fn||!version_fn||!execute_fn) return respond(FP_EXPORT_MISSING,NULL,0);
    if(!same_identity(abi_fn(),p[2])) return respond(FP_ABI,NULL,0);
    if(!same_identity(version_fn(),p[3])) return respond(FP_VERSION,NULL,0);
    if(fread(size_bytes,1,4,stdin)!=4) return respond(FP_INPUT,NULL,0);
    input_size=get32(size_bytes); if(input_size>max_input) return respond(FP_INPUT,NULL,0);
    input=(unsigned char *)malloc(input_size?input_size:1); output=(unsigned char *)malloc(max_output?(size_t)max_output:1);
    if(!input||!output) { free(input); free(output); return respond(FP_MEMORY,NULL,0); }
    if(fread(input,1,input_size,stdin)!=input_size||fgetc(stdin)!=EOF) { free(input); free(output); return respond(FP_INPUT,NULL,0); }
    status=execute_fn(input,input_size,output,(size_t)max_output,&output_size);
    if(status!=0||output_size>max_output) status=FP_ENGINE;
    else status=FP_OK;
#ifdef __APPLE__
    {
        struct rusage usage;
        if(getrusage(RUSAGE_SELF,&usage)!=0) status=FP_MEMORY;
        else if((uint64_t)usage.ru_maxrss>memory) status=FP_MEMORY;
    }
#endif
    free(input);
    status=respond((unsigned)status,status==FP_OK?output:NULL,status==FP_OK?(uint32_t)output_size:0);
    free(output);
    /* Do not run untrusted library destructors after returning the authenticated frame. */
    fflush(stdout);
#ifdef _WIN32
    ExitProcess((UINT)status);
#else
    _exit(status);
#endif
}

#ifdef _WIN32
int wmain(int argc, wchar_t **wide_arguments) {
    int i,result; char **arguments=(char **)calloc((size_t)argc+1,sizeof(char *));
    if(!arguments) return 92;
    for(i=0;i<argc;i++) {
        int size=WideCharToMultiByte(CP_UTF8,WC_ERR_INVALID_CHARS,wide_arguments[i],-1,NULL,0,NULL,NULL);
        if(!size) return 92;
        arguments[i]=(char *)malloc((size_t)size); if(!arguments[i]) return 92;
        if(!WideCharToMultiByte(CP_UTF8,WC_ERR_INVALID_CHARS,wide_arguments[i],-1,arguments[i],size,NULL,NULL)) return 92;
    }
    result=worker_main(argc,arguments);
    for(i=0;i<argc;i++) free(arguments[i]); free(arguments); return result;
}
#else
int main(int argc, char **argv) { return worker_main(argc,argv); }
#endif
