// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type {Vec3} from '../../core-sdk/room/recipe';
const number=(n:number)=>{const short=Number(n.toPrecision(7));return Math.fround(n)===Math.fround(short)?short:n;};
export function SweepPathEditor({path,onChange}:{path:Vec3[];onChange:(path:Vec3[])=>void}) {
 const change=(edit:(next:Vec3[])=>void)=>{const next=path.map(p=>({...p}));edit(next);onChange(next);};
 return <fieldset className="room-lathe-profile room-sweep-path" aria-label="Sweep path"><legend>Path through the part</legend>
  <p>The cross-section follows these points in order. Keep the path open; sharp bends need a thinner section. The finished shape must fit within −0.5 to 0.5 on each axis before dimension scaling.</p>
  <div className="room-sweep-previews">{(['y','z'] as const).map(axis=><figure key={axis}><figcaption>X / {axis.toUpperCase()} view</figcaption><svg role="img" aria-label={`Sweep path X ${axis.toUpperCase()}`} viewBox="-20 -20 240 240" width="240" height="240">
   <path d="M 0 100 H 200 M 100 0 V 200" stroke="#c9c0ad"/><polyline points={path.map(p=>`${(p.x+.5)*200},${(.5-p[axis])*200}`).join(' ')} fill="none" stroke="#174949" strokeWidth="2"/>
   {path.map((p,i)=><g key={i}><circle cx={(p.x+.5)*200} cy={(.5-p[axis])*200} r="3" fill="#174949"/><text x={(p.x+.5)*200+5} y={(.5-p[axis])*200+(i%2?15:-5)} fontSize="12">{i+1}</text></g>)}
  </svg></figure>)}</div>
  {path.map((point,index)=><div className="room-sweep-point" key={index}><span>Point {index+1}</span>{(['x','y','z'] as const).map(axis=><label key={axis}>{axis.toUpperCase()}<input aria-label={`Path point ${index+1} ${axis}`} type="number" min={-.5} max={.5} step={.01} value={number(point[axis])} onChange={e=>change(next=>{next[index][axis]=Number(e.target.value);})}/></label>)}
   <button aria-label={`Insert after path point ${index+1}`} disabled={path.length>=16} onClick={()=>change(next=>{const after=next[index+1],before=next[index-1]??point;const p={...point};for(const axis of ['x','y','z'] as const)p[axis]=after?(point[axis]+after[axis])/2:Math.max(-.5,Math.min(.5,point[axis]+(point[axis]-before[axis])/2));next.splice(index+1,0,p);})}>Insert after</button>
   <button aria-label={`Remove path point ${index+1}`} disabled={path.length<=2} onClick={()=>change(next=>{next.splice(index,1);})}>Remove</button>
  </div>)}
 </fieldset>;
}
