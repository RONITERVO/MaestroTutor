// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Programs;
using NUnit.Framework;

namespace Maestro.Quest.Tests
{
    public sealed class FactReadFailureTests
    {
        static BehaviourCatalog.FactDefinition Fact(ProgramType type,Func<BehaviourCatalog.FactContext,ProgramValue?> read)
            =>new("test.read",type,"Test fact","Synthetic native fact contract",read);

        [Test] public void MissingValueAndInvalidArgumentsStayUnavailableWithoutInventingData()
        {
            int reads=0;var fact=Fact(ProgramType.Text,_=>{reads++;return null;});
            Assert.That(fact.TryRead(default,out var value),Is.False);Assert.That(value.Type.Kind,Is.EqualTo(ProgramType.Void));
            Assert.That(fact.TryRead(default,2,null,out _),Is.False);
            Assert.That(fact.TryRead(default,1,new Newtonsoft.Json.Linq.JObject(),out _),Is.False);
            Assert.That(reads,Is.EqualTo(1),"Invalid arguments must not invoke the reader");
        }
        [Test] public void PresentFalseZeroAndEmptyTextRemainAvailable()
        {
            foreach(var value in new[]{new ProgramValue(false),new ProgramValue(0d),new ProgramValue("")}) {
                var fact=Fact(value.Type.Kind,_=>value);
                Assert.That(fact.TryRead(default,out var actual),Is.True);Assert.That(actual.Same(value),Is.True);
            }
        }
        [TestCase("long")][TestCase("control")][TestCase("number")][TestCase("type")]
        public void PresentInvalidValuesRaiseAControlledNativeFailure(string kind)
        {
            var value=kind switch {"long"=>new ProgramValue(new string('x',129)),"control"=>new ProgramValue("private\nvalue"),"number"=>new ProgramValue(double.NaN),_=>new ProgramValue(true)};
            var fact=Fact(kind=="number"||kind=="type"?ProgramType.Number:ProgramType.Text,_=>value);
            Assert.That(fact.ValidValue(value),Is.False,"The boolean validator remains available to callers");
            var error=Assert.Throws<ProgramFault>(()=>fact.TryRead(default,out _));
            Assert.That(error.Message,Is.EqualTo("Room fact is invalid: test.read"));
            Assert.That(error.Message,Does.Not.Contain("private"));
        }
        [Test] public void ReaderFailuresPropagateWithoutConvertingProgrammingErrorsToAbsence()
        {
            var controlled=new ProgramFault("Synthetic reader failure");
            Assert.That(Assert.Throws<ProgramFault>(()=>Fact(ProgramType.Text,_=>throw controlled).TryRead(default,out _)),Is.SameAs(controlled));
            var defect=new InvalidOperationException("Synthetic implementation defect");
            Assert.That(Assert.Throws<InvalidOperationException>(()=>Fact(ProgramType.Text,_=>throw defect).TryRead(default,out _)),Is.SameAs(defect));
        }
        sealed class World:IProgramFacts
        {
            internal bool ThrowInReader;
            public bool TryRead(string name,out ProgramValue value)
            {
                var definition=new BehaviourCatalog.FactDefinition("maestro.state",ProgramType.Text,"State","Test reader",_=>
                    ThrowInReader?throw new ProgramFault("Synthetic read budget failure"):new ProgramValue(new string('x',129)));
                return definition.TryRead(default,out value);
            }
        }
        [TestCase(false)][TestCase(true)]
        public void ProgramsStillFailAtTheReadWithoutCommittingAReplacementValue(bool throwInReader)
        {
            const string source="{\"version\":3,\"entry\":\"main\",\"resources\":[],\"state\":[{\"name\":\"observed\",\"initial\":\"unchanged\"}],\"events\":[],\"functions\":[{\"name\":\"main\",\"returns\":\"void\",\"parameters\":[],\"locals\":[],\"body\":[{\"id\":\"read\",\"op\":\"setState\",\"variable\":\"observed\",\"value\":{\"fact\":\"maestro.state\"}}]}]}";
            Assert.That(BehaviourProgram.TryParse(source,out var program,out var error),Is.True,error);
            var machine=new ProgramMachine(program,new World {ThrowInReader=throwInReader});
            Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Failed));Assert.That(machine.NodeId,Is.EqualTo("read"));
            Assert.That(machine.Error,Does.Contain(throwInReader?"Synthetic read budget failure":"Room fact is invalid"));
            Assert.That(machine.State["observed"].Text,Is.EqualTo("unchanged"));Assert.That(machine.Wait,Is.Null);
            Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Failed));
        }
    }
}
