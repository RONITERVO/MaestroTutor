// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#ifndef MAESTRO_UNDERWATER_INCLUDED
#define MAESTRO_UNDERWATER_INCLUDED
#define MAESTRO_WATER_MAX 16
int _MaestroUnderwaterCount;
float4x4 _MaestroWaterInverse[MAESTRO_WATER_MAX];
float4 _MaestroWaterShape[MAESTRO_WATER_MAX], _MaestroWaterPlane[MAESTRO_WATER_MAX], _MaestroWaterOptics[MAESTRO_WATER_MAX];
sampler2D _MaestroWaterHeights;
bool WaterClip(float at, float change, float limit, inout float enter, inout float leave) {
    if (abs(change) < 1e-8) return at <= limit;
    float t = (limit-at)/change;
    if (change > 0) leave = min(leave,t); else enter = max(enter,t);
    return enter <= leave;
}
float WaterHeight(int index, int2 position) {
    return tex2Dlod(_MaestroWaterHeights,float4((position.x+.5)/17.0,(index*17+position.y+.5)/(17.0*MAESTRO_WATER_MAX),0,0)).r;
}
float WaterBed(int index, float2 position, float4 shape) {
    int cells=(int)shape.w;float2 span=shape.xy*2;
    float2 grid=(position+shape.xy)/span*cells;
    int2 cell=clamp((int2)floor(grid),0,cells-1);float2 f=grid-cell;
    float a=WaterHeight(index,cell),b=WaterHeight(index,cell+int2(1,0)),c=WaterHeight(index,cell+int2(0,1)),d=WaterHeight(index,cell+1);
    return f.x+f.y<=1 ? a+(b-a)*f.x+(c-a)*f.y : d+(c-d)*(1-f.x)+(b-d)*(1-f.y);
}
bool WaterContains(int index, float3 position) {
    float4 s=_MaestroWaterShape[index],p=_MaestroWaterPlane[index];
    if(position.y<0||position.y>s.z||dot(position,p.xyz)>p.w)return false;
    if(s.w<.5)return dot(position.xz,position.xz)<=s.x*s.x;
    if(any(abs(position.xz)>s.xy))return false;
    return s.w<2||position.y>=WaterBed(index,position.xz,s);
}
bool WaterTriangle(float3 from,float3 delta,float2 origin,float2 slope,float height,
                   float2 cellOrigin,float2 cellSize,bool second,float begin,float end,out float2 interval) {
    float lo=begin,hi=end;float3 floorNormal=float3(slope.x,-1,slope.y);
    if(!WaterClip(dot(floorNormal,from),dot(floorNormal,delta),dot(slope,origin)-height,lo,hi)){interval=0;return false;}
    float direction=second?-1:1;
    float2 diagonal=direction/cellSize;
    if(!WaterClip(dot(diagonal,from.xz),dot(diagonal,delta.xz),direction+dot(diagonal,cellOrigin),lo,hi)){interval=0;return false;}
    interval=float2(lo,hi);return true;
}
float WaterTerrainLength(int index,float3 from,float3 delta,float enter,float leave,float4 shape) {
    int cells=(int)shape.w;float2 span=shape.xy*2,stepSize=span/cells;
    float3 start=from+delta*enter;
    int2 cell=clamp((int2)floor((start.xz+shape.xy)/span*cells),0,cells-1);
    int2 step=(int2)sign(delta.xz);float cursor=enter,wet=0;
    [loop]for(int visit=0;visit<35;visit++){
        float2 corner=(float2)cell/cells*span-shape.xy;
        int2 edge=cell+int2(step.x>0?1:0,step.y>0?1:0);
        float2 next=1e30;
        // The outer exit is already clipped; never recompute it with a
        // differently rounded expression and abandon a valid final cell.
        if(step.x!=0&&edge.x>0&&edge.x<cells)next.x=((float)edge.x/cells*span.x-shape.x-from.x)/delta.x;
        if(step.y!=0&&edge.y>0&&edge.y<cells)next.y=((float)edge.y/cells*span.y-shape.y-from.z)/delta.z;
        float end=max(cursor,min(leave,min(next.x,next.y)));
        float a=WaterHeight(index,cell),b=WaterHeight(index,cell+int2(1,0)),c=WaterHeight(index,cell+int2(0,1)),d=WaterHeight(index,cell+1);
        float2 one,two;
        bool hitOne=WaterTriangle(from,delta,corner,float2(b-a,c-a)/stepSize,a,corner,stepSize,false,cursor,end,one);
        bool hitTwo=WaterTriangle(from,delta,corner+stepSize,float2(d-c,d-b)/stepSize,d,corner,stepSize,true,cursor,end,two);
        if(hitOne)wet+=one.y-one.x;
        if(hitTwo)wet+=two.y-two.x;
        if(hitOne&&hitTwo)wet-=max(0,min(one.y,two.y)-max(one.x,two.x));
        if(end>=leave)return saturate(wet);
        if(next.x<=end)cell.x+=step.x;
        if(next.y<=end)cell.y+=step.y;
        cursor=end;
    }
    return 0; // Never expose a partial traversal as a completed path.
}
float WaterWetFraction(int index,float3 from,float3 to) {
    float3 delta=to-from;float4 s=_MaestroWaterShape[index],p=_MaestroWaterPlane[index];float enter=0,leave=1;
    if(!WaterClip(-from.y,-delta.y,0,enter,leave)||!WaterClip(from.y,delta.y,s.z,enter,leave)||
       !WaterClip(dot(from,p.xyz),dot(delta,p.xyz),p.w,enter,leave))return 0;
    if(s.w<.5){
        float aa=dot(delta.xz,delta.xz),bb=2*dot(from.xz,delta.xz),cc=dot(from.xz,from.xz)-s.x*s.x;
        if(aa==0){if(cc>0)return 0;}
        else{
            float discriminant=bb*bb-4*aa*cc;if(discriminant<0)return 0;
            float root=sqrt(discriminant);enter=max(enter,(-bb-root)/(2*aa));leave=min(leave,(-bb+root)/(2*aa));
            if(enter>leave)return 0;
        }
    }else{
        if(!WaterClip(from.x,delta.x,s.x,enter,leave)||!WaterClip(-from.x,-delta.x,s.x,enter,leave)||
           !WaterClip(from.z,delta.z,s.y,enter,leave)||!WaterClip(-from.z,-delta.z,s.y,enter,leave))return 0;
        if(s.w>=2)return WaterTerrainLength(index,from,delta,enter,leave,s);
    }
    return max(0,leave-enter);
}
// xyz is the authored linear tint, w is transmittance. Only the chosen
// containing cavity is projected: no infinite plane or full-screen tint.
float4 MaestroUnderwater(float3 world) {
    if(_MaestroUnderwaterCount<=0)return float4(0,0,0,1);
    float3 origin=world+UnityWorldSpaceViewDir(world);
    if(unity_OrthoParams.w>.5){
        float3 forward=-UNITY_MATRIX_V[2].xyz;
        origin=world-forward*dot(world-origin,forward);
    }
    [loop]for(int i=0;i<MAESTRO_WATER_MAX;i++){
        if(i>=_MaestroUnderwaterCount)break;
        float3 local=mul(_MaestroWaterInverse[i],float4(origin,1)).xyz;
        if(!WaterContains(i,local))continue;
        float4 optics=_MaestroWaterOptics[i];
        if(optics.w<=0)return float4(optics.rgb,1);
        float3 end=mul(_MaestroWaterInverse[i],float4(world,1)).xyz;
        float distance=length(world-origin)*WaterWetFraction(i,local,end);
        return float4(optics.rgb,exp(-optics.w*distance));
    }
    return float4(0,0,0,1);
}
#endif
