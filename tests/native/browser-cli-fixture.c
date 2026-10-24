#define _POSIX_C_SOURCE 200809L
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#ifdef _WIN32
#include <windows.h>
#include <io.h>
#include <fcntl.h>
#else
#include <time.h>
#endif

/* Original process/transport peer only. It creates no container and implements no HTML rendering. */
static int has(int argc, char **argv, const char *value) {
    int i; for(i=1;i<argc;i++) if(strcmp(argv[i],value)==0) return 1; return 0;
}
static void pause_forever(void) {
    for(;;) {
#ifdef _WIN32
        Sleep(1000);
#else
        struct timespec t={1,0}; nanosleep(&t,NULL);
#endif
    }
}
int main(int argc,char **argv) {
    int i, denied=0, pids=64;
    long long memory=268435456;
    char mode='a', line[8192];
#ifdef _WIN32
    (void)_setmode(_fileno(stdin),_O_BINARY);
    (void)_setmode(_fileno(stdout),_O_BINARY);
#endif
    for(i=1;i<argc;i++) {
        if(strstr(argv[i],"cleanup-denied")!=NULL) denied=1;
        if(strncmp(argv[i],"sha256:",7)==0 && strlen(argv[i])==71) mode=argv[i][70];
        if(strncmp(argv[i],"--env=FLUENT_PDF_MEMORY=",24)==0) memory=strtoll(argv[i]+24,NULL,10);
        if(strncmp(argv[i],"--env=FLUENT_PDF_PIDS=",22)==0) pids=atoi(argv[i]+22);
    }
    if(has(argc,argv,"rm") || has(argc,argv,"container")) return denied?12:0;
    if(!has(argc,argv,"run") || !has(argc,argv,"--network=none") || !has(argc,argv,"--read-only") || !has(argc,argv,"--cap-drop=ALL") || !has(argc,argv,"--security-opt=no-new-privileges") || !has(argc,argv,"--user=65532:65532") || !has(argc,argv,"--pull=never")) return 20;
    if(mode=='b') { puts("{}"); return 0; }
    printf("{\"fluentPdfBrowserHost\":1,\"memory\":%lld,\"pids\":%d,\"isolated\":true}\n",memory,pids);
    fflush(stdout);
    while(fgets(line,sizeof(line),stdin)!=NULL) {
        if(mode=='c') pause_forever();
        if(mode=='d') { fputs("{}\r\n",stdout); fflush(stdout); continue; }
        if(mode=='e') { fputs("private-sentinel-payload",stderr); return 7; }
        fputs(line,stdout); fflush(stdout);
    }
    return 0;
}
