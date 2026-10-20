/* Original bounded IPC adapter. Only public engine ABI declarations are reproduced. */
#include <stdint.h>
#include <stddef.h>
#include <stdlib.h>
#include <string.h>
#include "../worker/bridge.h"
#ifdef _WIN32
#define API __declspec(dllexport)
#else
#define API __attribute__((visibility("default")))
#endif
typedef struct font_info font_info;
struct font_info {
    int version;
    void (*release)(font_info *);
    void (*enumerate)(font_info *,void *);
    void *(*map)(font_info *,int,int,int,int,const char *,int *);
    void *(*get)(font_info *,const char *);
    unsigned long (*data)(font_info *,void *,unsigned,unsigned char *,unsigned long);
    unsigned long (*name)(font_info *,void *,char *,unsigned long);
    int (*charset)(font_info *,void *);
    void (*remove)(font_info *,void *);
};
/* Null callbacks never execute. Slot arrays describe the public 64-bit ABI,
   including disabled XFA callbacks, without importing engine implementation. */
typedef struct form_info form_info;
struct form_info {
    int version;
    void (*before_page[8])(void);
    void *(*get_page)(form_info *,void *,int);
    void (*after_page[6])(void);
    void *javascript;
    int xfa_disabled;
    void (*extended[17])(void);
    void *page; void *document; int page_index;
};
typedef struct {int version;const char **font_paths;void *isolate;unsigned slot;void *platform;int renderer;} library_config;
typedef struct pause_info pause_info;
struct pause_info {int version;int (*pause)(pause_info *);void *user;};
_Static_assert(sizeof(void *)==8,"Rendering requires a 64-bit isolated worker.");
_Static_assert(sizeof(font_info)==72,"Unexpected system-font ABI.");
_Static_assert(offsetof(form_info,get_page)==72 && offsetof(form_info,xfa_disabled)==136 && offsetof(form_info,page)==280,"Unexpected form ABI.");
extern void FPDF_InitLibraryWithConfig(const library_config *);
extern void FPDF_DestroyLibrary(void);
extern void FPDF_SetSystemFontInfo(font_info *);
extern void *FPDF_LoadMemDocument64(const void *,size_t,const char *);
extern void FPDF_CloseDocument(void *);
extern int FPDF_GetPageCount(void *);
extern void *FPDF_LoadPage(void *,int);
extern void FPDF_ClosePage(void *);
extern void *FPDFBitmap_CreateEx(int,int,int,void *,int);
extern int FPDFBitmap_FillRect(void *,int,int,int,int,unsigned long);
extern void FPDFBitmap_Destroy(void *);
extern int FPDF_RenderPageBitmap_Start(void *,void *,int,int,int,int,int,int,void *);
extern void FPDF_RenderPage_Close(void *);
extern void *FPDFDOC_InitFormFillEnvironment(void *,form_info *);
extern void FPDFDOC_ExitFormFillEnvironment(void *);
extern void FORM_OnAfterLoadPage(void *,void *);
extern void FORM_OnBeforeClosePage(void *,void *);
extern void FPDF_FFLDraw(void *,void *,void *,int,int,int,int,int,int);
extern int FPDFPage_GetAnnotCount(void *);
extern void *FPDFPage_GetAnnot(void *,int);
extern int FPDFAnnot_GetSubtype(void *);
extern int FPDFAnnot_GetFlags(void *);
extern int FPDFAnnot_SetFlags(void *,int);
extern void FPDFPage_CloseAnnot(void *);
static void *no_map(font_info *a,int b,int c,int d,int e,const char *f,int *g) {(void)a;(void)b;(void)c;(void)d;(void)e;(void)f;(void)g;return NULL;}
static void *no_font(font_info *a,const char *b) {(void)a;(void)b;return NULL;}
static unsigned long no_data(font_info *a,void *b,unsigned c,unsigned char *d,unsigned long e) {(void)a;(void)b;(void)c;(void)d;(void)e;return 0;}
static unsigned long no_name(font_info *a,void *b,char *c,unsigned long d) {(void)a;(void)b;(void)c;(void)d;return 0;}
static int no_charset(font_info *a,void *b) {(void)a;(void)b;return 0;}
static void no_delete(font_info *a,void *b) {(void)a;(void)b;}
static void *get_page(form_info *info,void *document,int index) {return document==info->document && index==info->page_index?info->page:NULL;}
static int no_pause(pause_info *info) {(void)info;return 0;}
static int widget_visibility(void *page,int printing)
{
    int count=FPDFPage_GetAnnotCount(page);if(count<0)return 0;
    for(int i=0;i<count;i++) {
        void *annotation=FPDFPage_GetAnnot(page,i);if(!annotation)return 0;
        if(FPDFAnnot_GetSubtype(annotation)==20) {
            int flags=FPDFAnnot_GetFlags(annotation);
            /* Invisible applies to unknown annotation types, not standard widgets. */
            int visible=!(flags&2) && (printing?!!(flags&4):!(flags&32));
            int adjusted=visible?(flags&~33):(flags|2);
            int ok=adjusted==flags || FPDFAnnot_SetFlags(annotation,adjusted);
            FPDFPage_CloseAnnot(annotation);if(!ok)return 0;
        }else FPDFPage_CloseAnnot(annotation);
    }
    return 1;
}
static uint32_t get(const uint8_t *p) {return (uint32_t)p[0]|(uint32_t)p[1]<<8|(uint32_t)p[2]<<16|(uint32_t)p[3]<<24;}
static void put(uint8_t *p,uint32_t v) {p[0]=(uint8_t)v;p[1]=(uint8_t)(v>>8);p[2]=(uint8_t)(v>>16);p[3]=(uint8_t)(v>>24);}
API const char *fluent_pdf_bridge_abi_v1(void) {return "fluent-pdf-render-1";}
API const char *fluent_pdf_bridge_version_v1(void) {return "153.0.7999.0";}
static int render(const uint8_t *input,size_t length,uint8_t *output,size_t capacity,size_t *written,fp_allocate_fn allocate,void *owner)
{
    void *document=NULL,*page=NULL,*bitmap=NULL,*form=NULL;
    int initialized=0,render_started=0,status=1,result=0;
    uint32_t width=0,height=0,stride=0; size_t extent=32;
    form_info forms;memset(&forms,0,sizeof(forms));
    pause_info pause={1,no_pause,NULL};
    if(!input||!written||(!output&&!allocate)||length<64||capacity<32)return 99;
    if(memcmp(input,"FPR1",4)||get(input+44)||get(input+48)||get(input+52)||get(input+56)||get(input+60))goto done;
    uint32_t pdf_length=get(input+4),index=get(input+8),full_width=get(input+12),full_height=get(input+16),x=get(input+20),y=get(input+24),flags=get(input+36),background=get(input+40);
    width=get(input+28);height=get(input+32);
    if(!pdf_length||length!=64ULL+pdf_length||index>2147483647||!full_width||!full_height||full_width>2147483647||full_height>2147483647||!width||!height||width>536870903||x>=full_width||y>=full_height||width>full_width-x||height>full_height-y||(flags&~2057u)||(background>>24!=0&&background>>24!=255))goto done;
    stride=width*4;
    if((uint64_t)stride*height>capacity-32){status=3;goto done;}
    extent=32+(size_t)stride*height;
    if(allocate)output=(uint8_t *)allocate(owner,extent);
    if(!output)return 99;
    const char *font_paths[]={NULL};
    library_config config;memset(&config,0,sizeof(config));config.version=4;config.font_paths=font_paths;config.renderer=0;
    FPDF_InitLibraryWithConfig(&config);initialized=1;
    font_info fonts;memset(&fonts,0,sizeof(fonts));fonts.version=2;fonts.map=no_map;fonts.get=no_font;fonts.data=no_data;fonts.name=no_name;fonts.charset=no_charset;fonts.remove=no_delete;
    FPDF_SetSystemFontInfo(&fonts);
    document=FPDF_LoadMemDocument64(input+64,pdf_length,NULL);
    if(!document||(int)index>=FPDF_GetPageCount(document)){status=4;goto done;}
    page=FPDF_LoadPage(document,(int)index);if(!page){status=4;goto done;}
    bitmap=FPDFBitmap_CreateEx((int)width,(int)height,4,output+32,(int)stride);
    if(!bitmap){status=3;goto done;}
    if(!FPDFBitmap_FillRect(bitmap,0,0,(int)width,(int)height,(unsigned long)background)){status=4;goto done;}
    if(flags&1u) {
        /* Form-fill drawing does not consistently apply print visibility.
           Adapt only this private worker's widget flags before page-view creation. */
        if(!widget_visibility(page,!!(flags&2048u))){status=4;goto done;}
        forms.version=2;forms.get_page=get_page;forms.xfa_disabled=1;forms.page=page;forms.document=document;forms.page_index=(int)index;
        form=FPDFDOC_InitFormFillEnvironment(document,&forms);if(!form){status=4;goto done;}
        FORM_OnAfterLoadPage(page,form);
    }
    render_started=1;
    if(FPDF_RenderPageBitmap_Start(bitmap,page,-(int)x,-(int)y,(int)full_width,(int)full_height,0,(int)flags,&pause)!=2){status=4;goto done;}
    if(form)FPDF_FFLDraw(form,bitmap,page,-(int)x,-(int)y,(int)full_width,(int)full_height,0,(int)flags);
    status=0;
done:
    if(render_started)FPDF_RenderPage_Close(page);
    if(form){FORM_OnBeforeClosePage(page,form);FPDFDOC_ExitFormFillEnvironment(form);}
    if(bitmap)FPDFBitmap_Destroy(bitmap);
    if(page)FPDF_ClosePage(page);
    if(document)FPDF_CloseDocument(document);
    if(initialized)FPDF_DestroyLibrary();
    if(!output&&allocate)output=(uint8_t *)allocate(owner,32);
    if(!output){result=99;goto release;}
    memset(output,0,32);memcpy(output,"FPRO",4);put(output+4,(uint32_t)status);*written=32;
    if(status==0){put(output+8,width);put(output+12,height);put(output+16,stride);put(output+20,4);*written=extent;}
release:
    return result;
}
API int fluent_pdf_bridge_execute_v1(const uint8_t *input,size_t length,uint8_t *output,size_t capacity,size_t *written)
{return render(input,length,output,capacity,written,NULL,NULL);}
API int fluent_pdf_bridge_execute_alloc_v1(const uint8_t *input,size_t length,size_t maximum,fp_allocate_fn allocate,void *owner,size_t *written)
{return render(input,length,NULL,maximum,written,allocate,owner);}
