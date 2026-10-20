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
struct original_api { int mode,width,height,partial;char languages[257]; };
struct original_monitor { bool (*cancel)(void *,int); int deadline; };
static int apis,monitors,texts,language_arrays;
API const char *TessVersion(void) { return "5.5.0-fixture"; }
API void *TessBaseAPICreate(void) { struct original_api *p=(struct original_api *)calloc(1,sizeof(*p));if(p)apis++;return p; }
API void TessBaseAPIDelete(void *p) { if(!p||apis!=1||monitors||texts||language_arrays)abort();apis--;free(p); }
API int TessBaseAPIInit3(void *p,const char *directory,const char *language) {
    struct original_api *api=(struct original_api *)p;char copy[257],*token;size_t language_length;
    if(!p||!language)return 1;
    language_length=strlen(language);if(language_length==0||language_length>256)return 1;
    memcpy(api->languages,language,language_length+1);memcpy(copy,language,language_length+1);
    token=strtok(copy,"+");
    while(token) {
        char path[16384],data[129]={0};FILE *file;int length=snprintf(path,sizeof(path),"%s/%s.traineddata",directory,token);
        if(length<0||(size_t)length>=sizeof(path))return 1;
        file=fopen(path,"rb");if(!file)return 1;
        {size_t read=fread(data,1,128,file);int failed=ferror(file);fclose(file);if(failed||read==0)return 1;}
        if(strncmp(data,"original-model",14))return 1;
        if(strstr(data,"partial"))api->partial=1;
        token=strtok(NULL,"+");
    }
    return 0;
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
API char **TessBaseAPIGetLoadedLanguagesAsVector(void *p) {
    struct original_api *api=(struct original_api *)p;char copy[257],*token;char **result=(char **)calloc(33,sizeof(char *));int count=0;
    if(!result)return NULL;
    memcpy(copy,api->languages,strlen(api->languages)+1);token=strtok(copy,"+");
    while(token&&count<32) {
        result[count]=copy_text(token);if(!result[count])abort();count++;if(api->partial)break;token=strtok(NULL,"+");
    }
    language_arrays++;return result;
}
API void TessDeleteTextArray(char **values) {
    if(!values||language_arrays!=1)abort();
    for(int i=0;values[i];i++){if(texts<1)abort();texts--;free(values[i]);}
    language_arrays--;free(values);
}
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
