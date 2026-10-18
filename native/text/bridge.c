/* Original bounded IPC/C-ABI adapter. HarfBuzz remains an explicitly pinned isolated dependency. */
#include <stdint.h>
#include <stddef.h>
#include <stdlib.h>
#include <string.h>
#ifdef _WIN32
#define API __declspec(dllexport)
#else
#define API __attribute__((visibility("default")))
#endif
/* Public HarfBuzz C ABI declarations only; no upstream algorithm code is included. */
typedef struct {uint32_t tag,value,start,end;} feature;
typedef struct {uint32_t glyph,mask,cluster,var1,var2;} info;
typedef struct {int32_t ax,ay,dx,dy;uint32_t var;} position;
extern const char *hb_version_string(void);
extern void *hb_blob_create(const char *,unsigned,int,void *,void (*)(void *));
extern void hb_blob_destroy(void *);
extern void *hb_face_create(void *,unsigned);
extern unsigned hb_face_get_upem(void *);
extern unsigned hb_face_get_glyph_count(void *);
extern void hb_face_destroy(void *);
extern void *hb_font_create(void *);
extern void hb_ot_font_set_funcs(void *);
extern void hb_font_set_scale(void *,int,int);
extern void hb_font_destroy(void *);
extern void *hb_buffer_create(void);
extern void hb_buffer_add_utf16(void *,const uint16_t *,int,unsigned,int);
extern void hb_buffer_set_direction(void *,int);
extern void hb_buffer_set_script(void *,uint32_t);
extern const void *hb_language_from_string(const char *,int);
extern void hb_buffer_set_language(void *,const void *);
extern void hb_buffer_set_cluster_level(void *,int);
extern void hb_buffer_set_flags(void *,unsigned);
extern int hb_buffer_allocation_successful(void *);
extern void hb_shape(void *,void *,const feature *,unsigned);
extern info *hb_buffer_get_glyph_infos(void *,unsigned *);
extern position *hb_buffer_get_glyph_positions(void *,unsigned *);
extern void hb_buffer_destroy(void *);
static uint32_t get(const uint8_t *p){return (uint32_t)p[0]|(uint32_t)p[1]<<8|(uint32_t)p[2]<<16|(uint32_t)p[3]<<24;}
static void put(uint8_t *p,uint32_t v){p[0]=(uint8_t)v;p[1]=(uint8_t)(v>>8);p[2]=(uint8_t)(v>>16);p[3]=(uint8_t)(v>>24);}
API const char *fluent_pdf_bridge_abi_v1(void){return "fluent-pdf-text-1";}
API const char *fluent_pdf_bridge_version_v1(void){return hb_version_string();}
API int fluent_pdf_bridge_execute_v1(const uint8_t *input,size_t length,uint8_t *output,size_t capacity,size_t *written)
{
    void *blob=NULL,*face=NULL,*font=NULL,*buffer=NULL;uint16_t *text=NULL;feature features[64];unsigned count=0,positions=0;int status=1;
    if(!written||!input||!output||length<48||capacity<16)return 99;
    memset(output,0,16);memcpy(output,"FPTO",4);*written=16;
    if(memcmp(input,"FPT1",4)||get(input+44)||strcmp(hb_version_string(),"14.2.1"))goto done;
    uint32_t font_length=get(input+4),text_length=get(input+8),face_index=get(input+12),start=get(input+16),run_length=get(input+20),direction=get(input+24),script=get(input+28),language_length=get(input+32),feature_count=get(input+36),maximum_glyphs=get(input+40);
    if(!font_length||font_length>67108864||text_length>16777216||start>text_length||run_length>text_length-start||!run_length||direction<4||direction>6||language_length<1||language_length>63||feature_count>64||maximum_glyphs>(capacity-16)/24||length!=48ULL+font_length+text_length*2ULL+language_length+feature_count*16ULL)goto done;
    text=(uint16_t *)malloc((size_t)text_length*2);if(!text){status=3;goto done;}
    const uint8_t *encoded=input+48+font_length;for(uint32_t i=0;i<text_length;i++)text[i]=(uint16_t)((unsigned)encoded[i*2]|(unsigned)encoded[i*2+1]<<8);
    const uint8_t *feature_bytes=encoded+text_length*2+language_length;
    for(uint32_t i=0;i<feature_count;i++){features[i].tag=get(feature_bytes+i*16);features[i].value=get(feature_bytes+i*16+4);features[i].start=get(feature_bytes+i*16+8);features[i].end=get(feature_bytes+i*16+12);}
    blob=hb_blob_create((const char *)input+48,font_length,1,NULL,NULL);face=hb_face_create(blob,face_index);unsigned upem=hb_face_get_upem(face);
    if(!hb_face_get_glyph_count(face)||upem<1||upem>16384)goto done;
    font=hb_font_create(face);hb_ot_font_set_funcs(font);hb_font_set_scale(font,(int)upem,(int)upem);
    buffer=hb_buffer_create();hb_buffer_add_utf16(buffer,text,(int)text_length,start,(int)run_length);hb_buffer_set_direction(buffer,(int)direction);hb_buffer_set_script(buffer,script);hb_buffer_set_language(buffer,hb_language_from_string((const char *)(encoded+text_length*2),(int)language_length));hb_buffer_set_cluster_level(buffer,0);hb_buffer_set_flags(buffer,8);
    if(!hb_buffer_allocation_successful(buffer)){status=3;goto done;}
    hb_shape(font,buffer,features,feature_count);
    if(!hb_buffer_allocation_successful(buffer)){status=3;goto done;}
    info *glyphs=hb_buffer_get_glyph_infos(buffer,&count);position *geometry=hb_buffer_get_glyph_positions(buffer,&positions);
    if(count!=positions||count>maximum_glyphs){status=3;goto done;}
    for(unsigned i=0;i<count;i++){uint8_t *p=output+16+(size_t)i*24;put(p,glyphs[i].glyph);put(p+4,glyphs[i].cluster);put(p+8,(uint32_t)geometry[i].ax);put(p+12,(uint32_t)geometry[i].ay);put(p+16,(uint32_t)geometry[i].dx);put(p+20,(uint32_t)geometry[i].dy);}
    put(output+8,count);put(output+12,upem);*written=16+(size_t)count*24;status=0;
done:
    if(buffer)hb_buffer_destroy(buffer);
    if(font)hb_font_destroy(font);
    if(face)hb_face_destroy(face);
    if(blob)hb_blob_destroy(blob);
    free(text);put(output+4,(uint32_t)status);return 0;
}
