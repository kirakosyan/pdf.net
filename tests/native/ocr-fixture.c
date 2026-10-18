/* Original C-API contract fixture, not a recognizer or a third-party engine.
   It aborts if API/monitor/text ownership is violated. */
#include <stdlib.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include <stdbool.h>
#ifdef _WIN32
#define API __declspec(dllexport)
#else
#define API __attribute__((visibility("default")))
#endif
struct original_api { int mode,width,height; };
struct original_monitor { bool (*cancel)(void *,int); int deadline; };
static int apis,monitors,texts;
API const char *TessVersion(void) { return "5.5.0-fixture"; }
API void *TessBaseAPICreate(void) { struct original_api *p=(struct original_api *)calloc(1,sizeof(*p));if(p)apis++;return p; }
API void TessBaseAPIDelete(void *p) { if(!p||apis!=1||monitors||texts)abort();apis--;free(p); }
API int TessBaseAPIInit3(void *p,const char *directory,const char *language) {
    char path[16384],data[16]={0};FILE *file;int length;
    if(!p||strcmp(language,"eng"))return 1;
    length=snprintf(path,sizeof(path),"%s/eng.traineddata",directory);if(length<0||(size_t)length>=sizeof(path))return 1;
    file=fopen(path,"rb");if(!file)return 1;(void)fread(data,1,15,file);fclose(file);return strncmp(data,"original-model",14)?1:0;
}
API void TessBaseAPISetImage(void *p,const unsigned char *data,int width,int height,int channels,int stride) {
    struct original_api *api=(struct original_api *)p;(void)channels;(void)stride;if(!p||!data)abort();api->mode=data[0];api->width=width;api->height=height;
}
API void TessBaseAPISetSourceResolution(void *p,int dpi) { if(!p||dpi<72||dpi>600)abort(); }
API void TessBaseAPISetPageSegMode(void *p,int mode) { if(!p||(mode!=3&&mode!=6&&mode!=7&&mode!=11))abort(); }
API void *TessMonitorCreate(void) { struct original_monitor *p=(struct original_monitor *)calloc(1,sizeof(*p));if(p)monitors++;return p; }
API void TessMonitorDelete(void *p) { if(!p||monitors!=1)abort();monitors--;free(p); }
API void TessMonitorSetCancelFunc(void *p,bool (*callback)(void *,int)) { ((struct original_monitor *)p)->cancel=callback; }
API void TessMonitorSetDeadlineMSecs(void *p,int milliseconds) { ((struct original_monitor *)p)->deadline=milliseconds; }
API int TessBaseAPIRecognize(void *p,void *m) {
    struct original_api *api=(struct original_api *)p;struct original_monitor *monitor=(struct original_monitor *)m;
    if(!p||!m||!monitor->cancel||monitor->deadline<1)abort();
    if(api->mode==1)return 1;
    if(api->mode==4){while(!monitor->cancel(NULL,0)){}return 1;}
    return 0;
}
static char *copy_text(const char *value) { size_t n=strlen(value);char *p=(char *)malloc(n+1);if(p){memcpy(p,value,n+1);texts++;}return p; }
API char *TessBaseAPIGetUTF8Text(void *p) { struct original_api *api=(struct original_api *)p;if(api->mode==2)return NULL;return copy_text(api->mode==6?"\xff":"AB\n"); }
#ifndef OMIT_TSV
API char *TessBaseAPIGetTsvText(void *p,int page) {
    struct original_api *api=(struct original_api *)p;char data[512];int length;if(page)abort();
    length=snprintf(data,sizeof(data),"1\t1\t0\t0\t0\t0\t0\t0\t%d\t%d\t-1\t\n5\t1\t1\t1\t1\t1\t%d\t1\t2\t2\t95\tAB\n",api->width,api->height,api->mode==3?100000:1);
    if(length<0||(size_t)length>=sizeof(data))abort();
    return copy_text(data);
}
#endif
API int TessBaseAPIMeanTextConf(void *p) { (void)p;return 95; }
API void TessDeleteText(void *p) { if(!p||texts<1)abort();texts--;free(p); }
