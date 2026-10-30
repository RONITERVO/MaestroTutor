// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type {DataValue,DataType,ScalarType} from './programValues';
export type Value=DataValue;
export type ValueType=DataType;
export type Expression={value:Value;type?:ValueType}|{var:string}|{state:string}|{fact:string;version?:number;arguments?:Record<string,unknown>;bindings?:Record<string,Expression>}|{op:string;args:Expression[]};
export interface ProgramCall {module?:string;function:string;args:Expression[];result?:string}
export type ProgramNode={id:string}&(
 {op:'awaitCondition';test:Expression;transition:'true'|'false'|'either';initial:'baseline'|'report';stableSeconds:Expression;timeout:Expression;received:string;value:string}|
 {op:'checkpoint'}|{op:'set'|'setState';variable:string;value:Expression}|{op:'forever';body:ProgramNode[]}|{op:'sleep';seconds:Expression}|{op:'awaitEvent';event:string;source:string;timeout:Expression;received:string;value:string;fields?:Record<string,string>;version?:number;arguments?:Record<string,unknown>;bindings?:Record<string,Expression>}|{op:'emitEvent';event:string;value:Expression}|{op:'if';test:Expression;then:ProgramNode[];else:ProgramNode[]}|
 {op:'repeat';count:Expression;body:ProgramNode[]}|{op:'switch';value:Expression;cases:{value:Value;body:ProgramNode[]}[];default:ProgramNode[]}|
 {op:'parallel';branches:ProgramCall[]}|{op:'call';module?:string;function:string;args:Expression[];result?:string}|{op:'return';value?:Expression}|
 {op:'invoke';capability:string;version:number;arguments:Record<string,unknown>;bindings:Record<string,Expression>;waitForChannels?:Expression;results?:Record<string,string>});
export interface ProgramFunction {name:string;returns:ValueType|'void';parameters:{name:string;type:ValueType}[];locals:{name:string;initial:Value;type?:ValueType}[];body:ProgramNode[]}
export interface BehaviourProgram {version:2|3;parallelVersion?:1;memoryVersion?:1;dataVersion?:1;moduleVersion?:1;imports?:ProgramImport[];entry:string;resources:string[];functions:ProgramFunction[];state?:{name:string;initial:Value;type?:ValueType;memory?:string}[];events?:{name:string;type:ScalarType}[]}
export interface ProgramModule {version:1;name:string;exports:string[];program:BehaviourProgram}
export interface ProgramImport {alias:string;hash:string;module:ProgramModule;signals:Record<string,string>}
