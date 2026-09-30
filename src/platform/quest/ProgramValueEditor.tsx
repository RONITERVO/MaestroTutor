// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {inferDataType,defaultDataValue,sameDataType,dataOperationType,dataTypeLabel} from '../../../shared/programValues';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {factArgumentType} from '../../../shared/behaviourFacts';
import {CapabilityFields,type EditorObject} from './CapabilityFields';
import {ProgramDataValueEditor} from './ProgramDataEditor';
import {programFacts,type Expression,type Value,type ValueType} from '../../core-sdk/room/programs';

export type ValueSource = {name:string; type:ValueType; kind:'var'|'state'|'fact'};
export function factExpression(name:string):Expression {const fact=behaviourFact(name);return fact?.input?{fact:name,version:fact.version,arguments:structuredClone(fact.example??{}),bindings:{}}:{fact:name};}
export const valueType=(value:Value,declared?:ValueType):ValueType=>declared??inferDataType(value);
export const defaultValue=defaultDataValue;
export const valueExpression=(type:ValueType,value=defaultValue(type)):Expression=>({value,...(typeof type==='object'?{type}:{})});
export function expressionType(value:Expression,sources:readonly ValueSource[]):ValueType {
  if('value' in value)return valueType(value.value,value.type);
  if('op' in value){const data=dataOperationType(value.op,value.args.map(v=>expressionType(v,sources)),value.args[1]&&'value' in value.args[1]?value.args[1].value:undefined);if(data)return data;return ['add','sub','mul','div','mod'].includes(value.op)?'number':'boolean';}
  return sources.find(s=>s.kind in value&&s.name===(value as unknown as Record<string,string>)[s.kind])?.type??'text';
}
const operators=[
  ['add','Add'],['sub','Subtract'],['mul','Multiply'],['div','Divide'],['mod','Remainder'],
  ['eq','Equals'],['ne','Does not equal'],['lt','Less than'],['le','At most'],['gt','Greater than'],['ge','At least'],
  ['and','Both true'],['or','Either true'],['not','Not']
];
export function ProgramValueEditor({value,type,sources,onChange,label,depth=0,objects=[]}:{
  value:Expression;type:ValueType;sources:readonly ValueSource[];onChange:(value:Expression)=>void;label:string;depth?:number;objects?:readonly EditorObject[];
}) {
  const kind='value' in value?'literal':'op' in value?'op:'+value.op:'var' in value?'var:'+value.var:'state' in value?'state:'+value.state:'fact:'+value.fact;
  const matching=sources.filter(s=>sameDataType(s.type,type));
  const dataOptions:{label:string;value:Expression}[]=[];
  for(const source of sources){const ref:Expression=source.kind==='fact'?factExpression(source.name):{[source.kind]:source.name} as Expression,t=source.type;
   if(typeof t==='string')continue;
   if('list' in t){
    if(type==='number')dataOptions.push({label:'Length of '+source.name,value:{op:'length',args:[ref]}});
    if(sameDataType(type,t.list))dataOptions.push({label:'Item from '+source.name,value:{op:'at',args:[ref,{value:0}]}});
    if(sameDataType(type,t)){for(const op of ['append','replace','remove'])dataOptions.push({label:op+' in '+source.name,value:{op,args:op==='append'?[ref,valueExpression(t.list)]:op==='replace'?[ref,{value:0},valueExpression(t.list)]:[ref,{value:0}]}});}
   }else{for(const [field,tField] of Object.entries(t.record)){
    if(sameDataType(type,tField))dataOptions.push({label:source.name+'.'+field,value:{op:'field',args:[ref,{value:field}]}});
    if(sameDataType(type,t))dataOptions.push({label:'Set '+source.name+'.'+field,value:{op:'withField',args:[ref,{value:field},valueExpression(tField)]}});
   }}
  }
  const dataOp='op' in value&&['length','at','append','replace','remove','field','withField'].includes(value.op);
  const ops=operators.filter(([op])=>type==='number'?['add','sub','mul','div','mod'].includes(op):type==='boolean'&&!['add','sub','mul','div','mod'].includes(op));
  const comparisonTypes:ValueType[]=[];for(const s of sources)if(typeof s.type==='object'&&!comparisonTypes.some(t=>sameDataType(t,s.type)))comparisonTypes.push(s.type);
  const operandType='op' in value?(value.op==='and'||value.op==='or'||value.op==='not'?'boolean':['eq','ne'].includes(value.op)?expressionType(value.args[0],sources):'number'):type;
  return <fieldset className="program-expression"><legend>{label}</legend>
    <label>Value source<select aria-label={label+' source'} value={kind} onChange={e=>{
      const [mode,...rest]=e.target.value.split(':'),name=rest.join(':');
      if(mode==='literal')onChange(valueExpression(type));
      else if(mode==='data')onChange(dataOptions[Number(name)].value);
      else if(mode==='op'){
        const childType=['and','or','not'].includes(name)?'boolean':'number';
        onChange({op:name,args:Array.from({length:name==='not'?1:2},()=>({value:defaultValue(childType)}))});
      }else onChange(mode==='fact'?factExpression(name):{[mode]:name} as Expression);
    }}><option value="literal">A value</option>{matching.map(s=><option key={s.kind+':'+s.name} value={s.kind+':'+s.name}>{s.kind==='state'?'State · ':s.kind==='fact'?'Room · ':'Variable · '}{s.name}</option>)}
      {dataOp&&<option value={'op:'+value.op}>{value.op}</option>}
      {depth<8&&dataOptions.map((v,i)=><option key={i} value={'data:'+i}>{v.label}</option>)}
      {depth<8&&ops.map(([op,name])=><option key={op} value={'op:'+op}>{name}</option>)}
    </select></label>
    {'fact' in value&&behaviourFact(value.fact)?.input&&(()=>{
      const definition=behaviourFact(value.fact)!;
      return <div className="program-fact-inputs"><p>{definition.description}</p>{Object.entries(definition.input!.properties??{}).map(([key,schema])=>{
       const binding=value.bindings?.[key],t=factArgumentType(value.fact,key),fieldLabel=label+' fact '+(schema.title??key);
       return <div key={key}>{t&&depth<8&&<label>{schema.title??key} input<select aria-label={fieldLabel+' mode'} value={binding?'expression':'literal'} onChange={e=>{const bindings={...value.bindings};if(e.target.value==='literal')delete bindings[key];else bindings[key]=valueExpression(t,value.arguments?.[key] as Value);onChange({...value,bindings});}}><option value="literal">Fixed value</option><option value="expression">Expression</option></select></label>}
        {binding&&t?<ProgramValueEditor objects={objects} label={fieldLabel+' expression'} value={binding} type={t} sources={sources} depth={depth+1} onChange={next=>onChange({...value,bindings:{...value.bindings,[key]:next}})}/>:<CapabilityFields schema={schema} value={value.arguments?.[key]} label={fieldLabel} objects={objects} onChange={next=>onChange({...value,arguments:{...value.arguments,[key]:next}})}/>}</div>;
      })}</div>;
    })()}
    {'value' in value&&<ProgramDataValueEditor label={label+' value'} value={value.value} type={type} onChange={v=>onChange(valueExpression(type,v))}/>}
    {dataOp&&'op' in value&&(()=>{
      const container=expressionType(value.args[0],sources);
      const change=(i:number,next:Expression)=>onChange({...value,args:value.args.map((v,j)=>i===j?next:v)});
      const key=value.args[1]&&'value' in value.args[1]?String(value.args[1].value):'';
      return <>
       <ProgramValueEditor objects={objects} value={value.args[0]} type={container} sources={sources} depth={depth+1} label={label+' collection'} onChange={v=>change(0,v)}/>
       {typeof container==='object'&&'record' in container?<><label>Record field<select aria-label={label+' field'} value={key} onChange={e=>{const args=[...value.args];args[1]={value:e.target.value};if(value.op==='withField'&&!sameDataType(container.record[key],container.record[e.target.value]))args[2]=valueExpression(container.record[e.target.value]);onChange({...value,args});}}>{Object.entries(container.record).filter(([,t])=>value.op==='withField'||sameDataType(t,type)).map(([k])=><option key={k} value={k}>{k}</option>)}</select></label>
        {value.op==='withField'&&<ProgramValueEditor objects={objects} label={label+' field value'} value={value.args[2]} type={container.record[key]} sources={sources} depth={depth+1} onChange={v=>change(2,v)}/>}</>:
        typeof container==='object'&&'list' in container&&<>
         {value.op!=='length'&&value.op!=='append'&&<ProgramValueEditor objects={objects} label={label+' index'} value={value.args[1]} type="number" sources={sources} depth={depth+1} onChange={v=>change(1,v)}/>}
         {(value.op==='append'||value.op==='replace')&&<ProgramValueEditor objects={objects} label={label+' item'} value={value.args[value.op==='append'?1:2]} type={container.list} sources={sources} depth={depth+1} onChange={v=>change(value.op==='append'?1:2,v)}/>}
        </>}
      </>;
    })()}
    {'op' in value&&!dataOp&&<>
      {['eq','ne'].includes(value.op)&&<label>Compare<select aria-label={label+' compared type'} value={typeof operandType==='string'?operandType:'structured:'+comparisonTypes.findIndex(t=>sameDataType(t,operandType))} onChange={e=>{const t=e.target.value.startsWith('structured:')?comparisonTypes[Number(e.target.value.split(':')[1])]:e.target.value as ValueType;onChange({...value,args:[valueExpression(t),valueExpression(t)]});}}>{typeof operandType==='object'&&!comparisonTypes.some(t=>sameDataType(t,operandType))&&<option value="structured:-1">{dataTypeLabel(operandType)}</option>}{comparisonTypes.map((t,i)=><option key={i} value={'structured:'+i}>{dataTypeLabel(t)}</option>)}<option value="number">Numbers</option><option value="text">Text</option><option value="boolean">True / false</option></select></label>}
      {value.args.map((arg,index)=><ProgramValueEditor objects={objects} key={index} value={arg} type={operandType} sources={sources} depth={depth+1} label={label+' '+(index===0?'left':'right')} onChange={next=>onChange({...value,args:value.args.map((v,i)=>i===index?next:v)})}/>)}
    </>}
  </fieldset>;
}
export const roomValueSources:ValueSource[]=Object.entries(programFacts).map(([name,type])=>({name,type,kind:'fact'}));
