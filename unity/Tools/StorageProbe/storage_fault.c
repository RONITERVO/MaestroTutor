// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Diagnostic APK only. No injection outside a single explicitly armed synthetic file.
#define _GNU_SOURCE
#include <errno.h>
#include <fcntl.h>
#include <limits.h>
#include <pthread.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/syscall.h>
#include <sys/stat.h>
#include <unistd.h>
#include <time.h>

static pthread_mutex_t gate = PTHREAD_MUTEX_INITIALIZER;
static char prefix[PATH_MAX], marker[PATH_MAX], file_prefix[128], detail[PATH_MAX+128], last_path[PATH_MAX];
static int armed, mode, hit, errors, calls, root_fd=-1;
static size_t consumed;
static const size_t limit = 7;

static int hex_id(const char *s) {
    if (strlen(s) != 32) return 0;
    for (int i=0;i<32;i++) if (!((s[i]>='0'&&s[i]<='9')||(s[i]>='a'&&s[i]<='f'))) return 0;
    return 1;
}
__attribute__((visibility("default"))) int maestro_fault_arm(const char *root, const char *name, int kind) {
    char real[PATH_MAX], parent[PATH_MAX], identity[33];
    if (!root || !name || (kind!=1 && kind!=2) || !realpath(root,real)) return 0;
    const char *base="/storage/emulated/0/Android/data/com.maestro.quest.storageprobe/files/storage-probe/";
    size_t n=strlen(base);
    if (strncmp(real,base,n) || strlen(real+n)!=42 || strcmp(real+n+32,"/workspace")) return 0;
    memcpy(identity,real+n,32); identity[32]=0;
    if (!hex_id(identity)) return 0;
    const char *names[]={"room.v20.json","program-memory.v1.json","room-snapshot.v19.json","room.v20.json.backup","program-memory.v1.json.backup"};
    int allowed=0;for(size_t i=0;i<sizeof(names)/sizeof(names[0]);i++) if(!strcmp(name,names[i])) allowed=1;
    if(!allowed) return 0;
    pthread_mutex_lock(&gate);
    if(armed){pthread_mutex_unlock(&gate);return 0;}
    snprintf(prefix,sizeof(prefix),"%s/%s.snapshot.",real,name);
    snprintf(parent,sizeof(parent),"%.*s",(int)(strlen(real)-10),real);
    snprintf(marker,sizeof(marker),"%s/native-hit.json",parent);
    snprintf(file_prefix,sizeof(file_prefix),"%s.snapshot.",name);
    root_fd=open(real,O_RDONLY|O_DIRECTORY|O_CLOEXEC|O_NOFOLLOW);
    if(root_fd<0){pthread_mutex_unlock(&gate);return 0;}
    mode=kind;hit=0;errors=0;calls=0;last_path[0]=0;consumed=0;armed=1;
    pthread_mutex_unlock(&gate);return 1;
}
__attribute__((visibility("default"))) int maestro_fault_disarm(void) {
    pthread_mutex_lock(&gate);armed=0;if(root_fd>=0){close(root_fd);root_fd=-1;}int count=errors;pthread_mutex_unlock(&gate);return count;
}
__attribute__((visibility("default"))) const char *maestro_fault_detail(void) {
    snprintf(detail,sizeof(detail),"calls=%d hit=%d errors=%d lastPath=%s",calls,hit,errors,last_path);return detail;
}
static void record_hit(const char *path,size_t requested) {
    char json[PATH_MAX+256], pending[PATH_MAX];
    int len=snprintf(json,sizeof(json),"{\"pid\":%d,\"path\":\"%s\",\"mode\":%d,\"prefixBytes\":%zu,\"requestedBytes\":%zu}",getpid(),path,mode,consumed,requested);
    snprintf(pending,sizeof(pending),"%s.pending",marker);
    int fd=(int)syscall(SYS_openat,AT_FDCWD,pending,O_WRONLY|O_CREAT|O_EXCL|O_CLOEXEC,0660);
    if(fd<0 || len<0 || len>=(int)sizeof(json)) _exit(92);
    if(syscall(SYS_write,fd,json,(size_t)len)!=len || syscall(SYS_fsync,fd) || syscall(SYS_close,fd) || syscall(SYS_renameat,AT_FDCWD,pending,AT_FDCWD,marker)) _exit(93);
}
static ssize_t intercept(int fd,const void *buf,size_t count,off_t offset,int positioned) {
    pthread_mutex_lock(&gate);
    char descriptor[64],path[PATH_MAX];ssize_t size=-1;
    if(armed && count){
        calls++;snprintf(descriptor,sizeof(descriptor),"/proc/self/fd/%d",fd);
        size=readlink(descriptor,path,sizeof(path)-1);if(size>=0){path[size]=0;snprintf(last_path,sizeof(last_path),"%s",path);}
    }
    // Quest's emulated-storage passthrough can annotate a live file descriptor
    // with " (deleted)". The virtual entry must still exist in our owned directory.
    const char *annotation=" (deleted)";size_t annotation_size=strlen(annotation);
    if(size>=(ssize_t)annotation_size && !strcmp(path+size-annotation_size,annotation))path[size-annotation_size]=0;
    const char *name=size>=0?strrchr(path,'/'):NULL;
    name=name?name+1:"";
    struct stat opened,owned;
    // Restrict to the exact synthetic staging path; never broaden to other files.
    // FUSE passthrough and virtual entries need not expose the same device/inode.
    int matches=size>=0 && !strncmp(path,prefix,strlen(prefix)) && !strncmp(name,file_prefix,strlen(file_prefix)) && hex_id(name+strlen(file_prefix))
        && !fstat(fd,&opened) && !fstatat(root_fd,name,&owned,AT_SYMLINK_NOFOLLOW)
        && S_ISREG(owned.st_mode) && S_ISREG(opened.st_mode);
    if(!matches){
        pthread_mutex_unlock(&gate);
        return positioned?syscall(SYS_pwrite64,fd,buf,count,offset):syscall(SYS_write,fd,buf,count);
    }
    if(consumed>=limit){errors++;pthread_mutex_unlock(&gate);errno=ENOSPC;return -1;}
    size_t take=count<limit-consumed?count:limit-consumed;
    ssize_t result=positioned?syscall(SYS_pwrite64,fd,buf,take,offset):syscall(SYS_write,fd,buf,take);
    if(result>0) consumed+=(size_t)result;
    if(consumed==limit && !hit){
        hit=1;char canonical[PATH_MAX];snprintf(canonical,sizeof(canonical),"%s%s",prefix,name+strlen(file_prefix));record_hit(canonical,count);
        if(mode==1){const struct timespec delay={3600,0};for(;;)syscall(SYS_nanosleep,&delay,NULL);}
    }
    pthread_mutex_unlock(&gate);return result;
}
__attribute__((visibility("default"))) ssize_t write(int fd,const void *buf,size_t count){return intercept(fd,buf,count,0,0);}
__attribute__((visibility("default"))) ssize_t pwrite(int fd,const void *buf,size_t count,off_t offset){return intercept(fd,buf,count,offset,1);}
__attribute__((visibility("default"))) ssize_t pwrite64(int fd,const void *buf,size_t count,off64_t offset){return intercept(fd,buf,count,(off_t)offset,1);}