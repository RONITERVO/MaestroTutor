// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {programFacts,type Expression,type Value,type ValueType} from '../../core-sdk/room/programs';

export type ValueSource = {name:string; type:ValueType; kind:'var'|'state'|'fact'};
export const valueType=(value:Value):ValueType=>typeof value==='string'?'text':typeof value as ValueType;
export const defaultValue=(type:ValueType):Value=>type==='boolean'?false:type==='text'?'':0;
export function expressionType(value:Expression,sources:readonly ValueSource[]):ValueType {
  if('value' in value)return valueType(value.value);
  if('op' in value)return ['add','sub','mul','div','mod'].includes(value.op)?'number':'boolean';
  return sources.find(s=>s.kind in value&&s.name===(value as unknown as Record<string,string>)[s.kind])?.type??'text';
}
const operators=[
  ['add','Add'],['sub','Subtract'],['mul','Multiply'],['div','Divide'],['mod','Remainder'],
  ['eq','Equals'],['ne','Does not equal'],['lt','Less than'],['le','At most'],['gt','Greater than'],['ge','At least'],
  ['and','Both true'],['or','Either true'],['not','Not']
];
export function ProgramValueEditor({value,type,sources,onChange,label,depth=0}:{
  value:Expression;type:ValueType;sources:readonly ValueSource[];onChange:(value:Expression)=>void;label:string;depth?:number;
}) {
  const kind='value' in value?'literal':'op' in value?'op:'+value.op:'var' in value?'var:'+value.var:'state' in value?'state:'+value.state:'fact:'+value.fact;
  const matching=sources.filter(s=>s.type===type);
  const ops=operators.filter(([op])=>type==='number'?['add','sub','mul','div','mod'].includes(op):type==='boolean'&&!['add','sub','mul','div','mod'].includes(op));
  const operandType='op' in value?(value.op==='and'||value.op==='or'||value.op==='not'?'boolean':['eq','ne'].includes(value.op)?expressionType(value.args[0],sources):'number'):type;
  return <fieldset className="program-expression"><legend>{label}</legend>
    <label>Value source<select aria-label={label+' source'} value={kind} onChange={e=>{
      const [mode,...rest]=e.target.value.split(':'),name=rest.join(':');
      if(mode==='literal')onChange({value:defaultValue(type)});
      else if(mode==='op'){
        const childType=['and','or','not'].includes(name)?'boolean':'number';
        onChange({op:name,args:Array.from({length:name==='not'?1:2},()=>({value:defaultValue(childType)}))});
      }else onChange({[mode]:name} as Expression);
    }}><option value="literal">A value</option>{matching.map(s=><option key={s.kind+':'+s.name} value={s.kind+':'+s.name}>{s.kind==='state'?'State · ':s.kind==='fact'?'Room · ':'Variable · '}{s.name}</option>)}
      {depth<8&&ops.map(([op,name])=><option key={op} value={'op:'+op}>{name}</option>)}
    </select></label>
    {'value' in value&&(type==='boolean'?<label>{label}<select aria-label={label+' value'} value={String(value.value)} onChange={e=>onChange({value:e.target.value==='true'})}><option value="true">True</option><option value="false">False</option></select></label>:
      <label>{label}<input aria-label={label+' value'} type={type==='number'?'number':'text'} step="any" min={type==='number'?-1000000:undefined} max={type==='number'?1000000:undefined} maxLength={128} value={String(value.value)} onChange={e=>onChange({value:type==='number'?Number(e.target.value):e.target.value})}/></label>)}
    {'op' in value&&<>
      {['eq','ne'].includes(value.op)&&<label>Compare<select aria-label={label+' compared type'} value={operandType} onChange={e=>onChange({...value,args:[{value:defaultValue(e.target.value as ValueType)},{value:defaultValue(e.target.value as ValueType)}]})}><option value="number">Numbers</option><option value="text">Text</option><option value="boolean">True / false</option></select></label>}
      {value.args.map((arg,index)=><ProgramValueEditor key={index} value={arg} type={operandType} sources={sources} depth={depth+1} label={label+' '+(index===0?'left':'right')} onChange={next=>onChange({...value,args:value.args.map((v,i)=>i===index?next:v)})}/>)}
    </>}
  </fieldset>;
}
export const roomValueSources:ValueSource[]=Object.entries(programFacts).map(([name,type])=>({name,type,kind:'fact'}));
