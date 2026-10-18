/* Original bounded OCR IPC implementation.
   Narrow loader/API/monitor/buffer ownership patterns adapted from WN01,
   commit 141236a3fe890d220b807a270cec2d86a8f7e3b5 (MIT).
   Copyright (c) 2026 Armen Kirakosyan. See third-party/ocr/WN01-MIT.txt.
   No upstream document-model dependency or implicit native/model discovery. */
#ifndef _GNU_SOURCE
#define _GNU_SOURCE
#endif
#include <stdint.h>
#include <stddef.h>
#include <stdlib.h>
#include <string.h>
#include <stdbool.h>
#include <time.h>
#include "../worker/bridge.h"
#ifdef _WIN32
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <wchar.h>
#define API __declspec(dllexport)
#else
#include <dlfcn.h>
#include <stdio.h>
#define API __attribute__((visibility("default")))
#endif
API const char *fluent_pdf_bridge_abi_v1(void) { return "fluent-pdf-ocr-1"; }
API const char *fluent_pdf_bridge_version_v1(void) { return "1.0"; }
typedef const char *(*version_fn)(void);
typedef void *(*create_fn)(void);
typedef void (*delete_fn)(void *);
typedef int (*init_fn)(void *,const char *,const char *);
typedef void (*image_fn)(void *,const unsigned char *,int,int,int,int);
typedef void (*integer_fn)(void *,int);
typedef int (*recognize_fn)(void *,void *);
typedef char *(*text_fn)(void *);
typedef char *(*page_text_fn)(void *,int);
typedef int (*confidence_fn)(void *);
typedef bool (*cancel_fn)(void *,int);
typedef void (*set_cancel_fn)(void *,cancel_fn);
struct engine_api {
    version_fn version; create_fn create; delete_fn destroy; init_fn init;
    image_fn image; integer_fn resolution,segmentation; recognize_fn recognize;
    text_fn text; page_text_fn tsv; confidence_fn confidence; delete_fn delete_text;
    create_fn monitor; delete_fn delete_monitor; set_cancel_fn set_cancel; integer_fn deadline;
};
static const char *symbols[]={"TessVersion","TessBaseAPICreate","TessBaseAPIDelete","TessBaseAPIInit3",
    "TessBaseAPISetImage","TessBaseAPISetSourceResolution","TessBaseAPISetPageSegMode","TessBaseAPIRecognize",
    "TessBaseAPIGetUTF8Text","TessBaseAPIGetTsvText","TessBaseAPIMeanTextConf","TessDeleteText",
    "TessMonitorCreate","TessMonitorDelete","TessMonitorSetCancelFunc","TessMonitorSetDeadlineMSecs"};
static uint32_t get(const unsigned char *p) { return (uint32_t)p[0]|(uint32_t)p[1]<<8|(uint32_t)p[2]<<16|(uint32_t)p[3]<<24; }
static void put(unsigned char *p,uint32_t n) { p[0]=(unsigned char)n;p[1]=(unsigned char)(n>>8);p[2]=(unsigned char)(n>>16);p[3]=(unsigned char)(n>>24); }
static size_t extent(const char *s,size_t maximum) { size_t n=0;if(!s)return SIZE_MAX;while(n<=maximum&&s[n])n++;return n; }
static uint64_t milliseconds(void) {
#ifdef _WIN32
    return (uint64_t)GetTickCount64();
#else
    struct timespec t;if(clock_gettime(CLOCK_MONOTONIC,&t)!=0)return UINT64_MAX;
    return (uint64_t)t.tv_sec*1000u+(uint64_t)t.tv_nsec/1000000u;
#endif
}
/* Each bridge process executes one request; callback state never crosses jobs. */
static const char tsv_header[]="level\tpage_num\tblock_num\tpar_num\tline_num\tword_num\tleft\ttop\twidth\theight\tconf\ttext\n";
static uint64_t recognition_deadline;
static bool cancel_recognition(void *state,int words) { (void)state;(void)words;return milliseconds()>=recognition_deadline; }
static int bind_engine(struct engine_api *api,char *directory,size_t capacity,uint32_t *missing) {
    void *address=NULL;fp_identity_fn own=fluent_pdf_bridge_version_v1;
    _Static_assert(sizeof(own)==sizeof(address),"Unexpected function pointer representation");
    memcpy(&address,&own,sizeof(address));
#ifdef _WIN32
    HMODULE self,module;wchar_t wide[8192],engine[8192];DWORD count;int bytes;
    if(!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS|GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,(LPCWSTR)address,&self))return 1;
    count=GetModuleFileNameW(self,wide,8192);if(count==0||count>=8192)return 1;
    while(count>0&&wide[count-1]!=L'\\'&&wide[count-1]!=L'/')count--;
    if(count==0||count+15>=8192)return 1;
    wide[count]=0;
    bytes=WideCharToMultiByte(CP_UTF8,WC_ERR_INVALID_CHARS,wide,-1,directory,(int)capacity,NULL,NULL);if(bytes<=0)return 1;
    memcpy(engine,wide,(size_t)(count+1)*sizeof(wchar_t));memcpy(engine+count,L"tesseract.dll",sizeof(L"tesseract.dll"));
    module=LoadLibraryExW(engine,NULL,LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR|LOAD_LIBRARY_SEARCH_SYSTEM32);if(!module)return 1;
#else
    Dl_info info;char path[8192];size_t count;
    if(!dladdr(address,&info)||!info.dli_fname)return 1;
    count=strlen(info.dli_fname);if(count==0||count>=capacity)return 1;
    memcpy(directory,info.dli_fname,count+1);while(count>0&&directory[count-1]!='/')count--;if(count==0)return 1;directory[count]=0;
#ifdef __APPLE__
    if(snprintf(path,sizeof(path),"%slibtesseract.dylib",directory)<0)return 1;
#else
    if(snprintf(path,sizeof(path),"%slibtesseract.so",directory)<0)return 1;
#endif
    if(strlen(path)>=sizeof(path)-1)return 1;
    void *module=dlopen(path,RTLD_LOCAL|RTLD_NOW);if(!module)return 1;
#endif
    /* Retain the module until fresh process exit; native TLS/threads may outlive API deletion. */
#define BIND(field,index) do { \
    _Static_assert(sizeof(api->field)==sizeof(address),"Unexpected native function pointer size"); \
    address=NULL; \
    LOAD_SYMBOL(index); \
    if(!address){*missing=(uint32_t)(index);return 2;} \
    memcpy(&api->field,&address,sizeof(address)); \
} while(0)
#ifdef _WIN32
#define LOAD_SYMBOL(index) do { FARPROC proc=GetProcAddress(module,symbols[index]);memcpy(&address,&proc,sizeof(address)); } while(0)
#else
#define LOAD_SYMBOL(index) do { address=dlsym(module,symbols[index]); } while(0)
#endif
    BIND(version,0);BIND(create,1);BIND(destroy,2);BIND(init,3);BIND(image,4);BIND(resolution,5);
    BIND(segmentation,6);BIND(recognize,7);BIND(text,8);BIND(tsv,9);BIND(confidence,10);BIND(delete_text,11);
    BIND(monitor,12);BIND(delete_monitor,13);BIND(set_cancel,14);BIND(deadline,15);
#undef BIND
#undef LOAD_SYMBOL
    return 0;
}
static int recognize(const unsigned char *input,size_t length,unsigned char *output,size_t capacity,size_t *written,fp_allocate_fn allocate,void *owner) {
    struct engine_api api;void *engine=NULL,*monitor=NULL;char *text=NULL,*tsv=NULL;unsigned char *gray=NULL;
    char directory[8192],expected[129],language[257];const char *actual=NULL;size_t actual_size=0,text_size=0,tsv_size=0,total=32;
    uint32_t status=0,detail=0,width,height,stride,format,dpi,psm,max_chars,max_words,max_tsv,version_size,language_size,deadline,max_utf8,confidence=0;
    uint64_t pixels,raw_length,now;const unsigned char *samples;int result;
    memset(&api,0,sizeof(api));if(!input||length<64||memcmp(input,"FPO1",4)!=0)return 1;
    width=get(input+4);height=get(input+8);stride=get(input+12);format=get(input+16);dpi=get(input+20);psm=get(input+24);
    max_chars=get(input+28);max_words=get(input+32);max_tsv=get(input+36);version_size=get(input+40);language_size=get(input+44);deadline=get(input+48);max_utf8=get(input+52);
    pixels=(uint64_t)width*height;raw_length=(uint64_t)stride*height;
    if(width==0||height==0||width>100000||height>100000||(format!=1&&format!=3&&format!=4)||stride<(uint64_t)width*format||stride>(uint64_t)width*format+65536||
        dpi<72||dpi>600||(psm!=3&&psm!=6&&psm!=7&&psm!=11)||max_chars==0||max_chars>1000000||max_words==0||max_words>1000000||max_tsv==0||max_tsv>16777216||
        version_size==0||version_size>128||language_size!=3||deadline==0||deadline>86400000||max_utf8!=max_chars*4u||get(input+56)||get(input+60)||
        raw_length>SIZE_MAX||64u+(uint64_t)version_size+language_size+raw_length!=length)return 1;
    memcpy(expected,input+64,version_size);expected[version_size]=0;memcpy(language,input+64+version_size,language_size);language[language_size]=0;
    if(memchr(expected,0,version_size)||strcmp(language,"eng")!=0)return 1;
    samples=input+64+version_size+language_size;
    status=(uint32_t)bind_engine(&api,directory,sizeof(directory),&detail);if(status)goto done;
    actual=api.version();actual_size=extent(actual,128);if(actual_size==0||actual_size>128||actual_size!=version_size||memcmp(actual,expected,actual_size)!=0){status=3;goto done;}
    engine=api.create();if(!engine){status=5;goto done;}
    /* The explicit loaded bridge asset directory contains only caller-pinned models; no cwd/env/default tessdata lookup. */
    if(api.init(engine,directory,language)!=0){status=4;goto done;}
    if(format==4) {
        if(pixels>SIZE_MAX){status=5;goto done;}gray=(unsigned char *)malloc((size_t)pixels);if(!gray){status=5;goto done;}
        for(uint32_t y=0;y<height;y++)for(uint32_t x=0;x<width;x++) {
            const unsigned char *p=samples+(size_t)y*stride+(size_t)x*4;if(p[3]!=255){status=6;goto done;}
            gray[(size_t)y*width+x]=(unsigned char)(((uint32_t)p[2]*77u+(uint32_t)p[1]*150u+(uint32_t)p[0]*29u+128u)>>8);
        }
        api.image(engine,gray,(int)width,(int)height,1,(int)width);
    }else api.image(engine,samples,(int)width,(int)height,(int)format,(int)stride);
    api.resolution(engine,(int)dpi);api.segmentation(engine,(int)psm);monitor=api.monitor();if(!monitor){status=5;goto done;}
    now=milliseconds();if(now==UINT64_MAX||UINT64_MAX-now<deadline){status=8;goto done;}recognition_deadline=now+deadline;
    api.set_cancel(monitor,cancel_recognition);api.deadline(monitor,(int)deadline);result=api.recognize(engine,monitor);
    if(cancel_recognition(NULL,0)){status=8;goto done;}if(result!=0){status=6;goto done;}
    text=api.text(engine);tsv=api.tsv(engine,0);if(!text||!tsv){status=5;goto done;}
    text_size=extent(text,max_utf8);tsv_size=extent(tsv,max_tsv);if(text_size>max_utf8||max_tsv<sizeof(tsv_header)-1||tsv_size>max_tsv-(sizeof(tsv_header)-1)){status=7;goto done;}
    result=api.confidence(engine);if(result<0||result>100){status=6;goto done;}confidence=(uint32_t)result;
    if(actual_size>SIZE_MAX-32||text_size>SIZE_MAX-32-actual_size||tsv_size>SIZE_MAX-32-actual_size-text_size-(sizeof(tsv_header)-1)){status=7;goto done;}
    total=32+actual_size+text_size+(sizeof(tsv_header)-1)+tsv_size;
done:
    if(status)total=32;
    if(allocate){output=(unsigned char *)allocate(owner,total);if(!output)goto allocation_failed;}else if(!output||capacity<total)goto allocation_failed;
    memset(output,0,32);memcpy(output,"FPOO",4);put(output+4,status);put(output+8,detail);put(output+12,confidence);
    if(!status){put(output+16,(uint32_t)text_size);put(output+20,(uint32_t)(tsv_size+sizeof(tsv_header)-1));put(output+24,(uint32_t)actual_size);
        memcpy(output+32,actual,actual_size);memcpy(output+32+actual_size,text,text_size);memcpy(output+32+actual_size+text_size,tsv_header,sizeof(tsv_header)-1);memcpy(output+32+actual_size+text_size+sizeof(tsv_header)-1,tsv,tsv_size);}
    *written=total;
    if(tsv&&api.delete_text)api.delete_text(tsv);
    if(text&&api.delete_text)api.delete_text(text);
    if(monitor&&api.delete_monitor)api.delete_monitor(monitor);
    if(engine&&api.destroy)api.destroy(engine);
    free(gray);return 0;
allocation_failed:
    if(tsv&&api.delete_text)api.delete_text(tsv);
    if(text&&api.delete_text)api.delete_text(text);
    if(monitor&&api.delete_monitor)api.delete_monitor(monitor);
    if(engine&&api.destroy)api.destroy(engine);
    free(gray);return 1;
}
API int fluent_pdf_bridge_execute_v1(const unsigned char *input,size_t length,unsigned char *output,size_t capacity,size_t *written) {return recognize(input,length,output,capacity,written,NULL,NULL);}
API int fluent_pdf_bridge_execute_alloc_v1(const unsigned char *input,size_t length,size_t capacity,fp_allocate_fn allocate,void *owner,size_t *written) {return recognize(input,length,NULL,capacity,written,allocate,owner);}
