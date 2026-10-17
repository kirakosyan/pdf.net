#include "../../native/worker/bridge.h"
#include <stdlib.h>
#include <string.h>
#include <stdio.h>
#include <stdint.h>
#include <wchar.h>
#ifdef _WIN32
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <winsock2.h>
#else
#include <unistd.h>
#include <sys/socket.h>
#include <netinet/in.h>
#include <sys/wait.h>
#endif
#ifndef FIXTURE_ABI
#define FIXTURE_ABI "fluent-pdf-fixture-1"
#endif
FP_EXPORT const char *fluent_pdf_bridge_abi_v1(void) { return FIXTURE_ABI; }
FP_EXPORT const char *fluent_pdf_bridge_version_v1(void) { return "1.0.0"; }
#ifdef FIXTURE_DEPENDENCY
extern int fixture_dependency(void);
#endif
FP_EXPORT int fluent_pdf_bridge_execute_v1(const unsigned char *input,size_t length,unsigned char *output,size_t capacity,size_t *written) {
    if(length==0) { *written=0; return 0; }
#ifdef FIXTURE_DEPENDENCY
    if(fixture_dependency()!=42) return 99;
#endif
    switch(input[0]) {
        case 1: *(volatile int *)(uintptr_t)0=1; return 99;
        case 2:
            for(;;) {
#ifdef _WIN32
                Sleep(1000);
#else
                sleep(1);
#endif
            }
        case 3: *written=capacity+1; return 0;
        case 4: {
            FILE *file;
            if(length<2||input[length-1]!=0||capacity<1) return 99;
            file=fopen((const char *)input+1,"wb");
            output[0]=(unsigned char)(file!=NULL); if(file) { fputs("corrupt",file); fclose(file); }
            *written=1; return 0;
        }
        case 5: {
#ifdef _WIN32
            WSADATA data; SOCKET connection;
            if(capacity<1 || length!=3) return 99;
            if(WSAStartup(MAKEWORD(2,2),&data)!=0) { output[0]=0; *written=1; return 0; }
            connection=socket(AF_INET,SOCK_STREAM,IPPROTO_TCP);
            /* AppContainer can create a socket, but cannot connect without a capability. */
            if(connection!=INVALID_SOCKET) {
                struct sockaddr_in address; int result;u_long nonblocking=1;
                if(ioctlsocket(connection,FIONBIO,&nonblocking)!=0) {closesocket(connection);WSACleanup();return 99;}
                memset(&address,0,sizeof(address)); address.sin_family=AF_INET; address.sin_port=htons((unsigned short)((unsigned)input[1]*256+input[2])); address.sin_addr.s_addr=htonl(0x7f000001);
                result=connect(connection,(struct sockaddr *)&address,sizeof(address));
                output[0]=(unsigned char)(result==0);
                if(result!=0 && WSAGetLastError()==WSAEWOULDBLOCK) {
                    fd_set writable,failed;struct timeval timeout={1,0};
                    FD_ZERO(&writable);FD_ZERO(&failed);FD_SET(connection,&writable);FD_SET(connection,&failed);
                    if(select(0,NULL,&writable,&failed,&timeout)>0 && FD_ISSET(connection,&writable)) {
                        int error=0,size=sizeof(error);
                        if(getsockopt(connection,SOL_SOCKET,SO_ERROR,(char *)&error,&size)==0 && error==0)output[0]=1;
                    }
                }
                closesocket(connection);
            } else output[0]=0;
            WSACleanup();
#else
            int connection=socket(AF_INET,SOCK_STREAM,0);
            if(capacity<1 || length!=3) return 99;
            output[0]=0;
            if(connection>=0) {
                struct sockaddr_in address;
                memset(&address,0,sizeof(address)); address.sin_family=AF_INET;
                address.sin_port=htons((unsigned short)((unsigned)input[1]*256+input[2])); address.sin_addr.s_addr=htonl(0x7f000001);
                output[0]=(unsigned char)(connect(connection,(struct sockaddr *)&address,sizeof(address))==0);
                close(connection);
            }
#endif
            *written=1; return 0;
        }
        case 6: {
            volatile unsigned char *memory; size_t size=(size_t)512*1024*1024,i; int allocated;
            if(capacity<1) return 99;
            memory=(volatile unsigned char *)malloc(size);
            allocated=memory!=NULL;
            if(memory) { for(i=0;i<size;i+=4096) memory[i]=1; free((void *)memory); }
            output[0]=(unsigned char)allocated; *written=1; return 0;
        }
        case 7: {
            FILE *file;
            if(length<2 || input[length-1]!=0 || capacity<2) return 99;
            file=fopen((const char *)input+1,"rb");
            output[0]=(unsigned char)(file!=NULL); if(file) fclose(file);
#ifdef _WIN32
            {
                wchar_t executable[MAX_PATH],command[MAX_PATH+32];
                STARTUPINFOW startup; PROCESS_INFORMATION child;
                if(!GetSystemDirectoryW(executable,MAX_PATH-10)) return 99;
                wcscat(executable,L"\\cmd.exe");
                swprintf(command,MAX_PATH+32,L"\"%ls\" /d /c exit 0",executable);
                memset(&startup,0,sizeof(startup)); memset(&child,0,sizeof(child)); startup.cb=sizeof(startup);
                output[1]=(unsigned char)(CreateProcessW(executable,command,NULL,NULL,FALSE,CREATE_NO_WINDOW,NULL,NULL,&startup,&child)!=0);
                if(output[1]) { WaitForSingleObject(child.hProcess,1000); TerminateProcess(child.hProcess,0); CloseHandle(child.hProcess); CloseHandle(child.hThread); }
            }
#else
            {
                pid_t child=fork();
                if(child==0) _exit(0);
                output[1]=(unsigned char)(child>0); if(child>0) waitpid(child,NULL,0);
            }
#endif
            *written=2; return 0;
        }
        default:
            if(length-1>capacity) return 99;
            memcpy(output,input+1,length-1); *written=length-1; return 0;
    }
}
