using System;
using System.Linq;
using GomokuGame.AI.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GomokuGame.Tests
{
    [TestClass]
    public class TopologyParserTests
    {
        [TestMethod]
        public void Parse_MissingParameter_ReturnsDefault()
        {
            var topology = TopologyParser.Parse(null);

            Assert.AreEqual(1, topology.Stages.Count);

            var stage = topology.Stages[0];

            CollectionAssert.AreEqual(
                new[] { 324, 128, 64, 1 },
                stage.Sizes.ToArray());

            CollectionAssert.AreEqual(
                new[] { "Grade" },
                stage.OutputNames.ToArray());

            Assert.AreEqual("Оценка позиции", stage.TrainerName);
            Assert.AreEqual(324, stage.InputSize);
            Assert.AreEqual(1, stage.OutputSize);
        }

        [TestMethod]
        public void Parse_TwoNetworks_PreservesTheirDescriptions()
        {
            var topology = TopologyParser.Parse(
                "{324,128,64,2}[My,Opp](MyVsOwnerGradeMain)" +
                "->{2,2,1}[Grade](MyVsOwnerGradeBase)");

            Assert.AreEqual(2, topology.Stages.Count);

            var main = topology.Stages[0];
            var aggregation = topology.Stages[1];

            CollectionAssert.AreEqual(
                new[] { 324, 128, 64, 2 },
                main.Sizes.ToArray());

            CollectionAssert.AreEqual(
                new[] { "My", "Opp" },
                main.OutputNames.ToArray());

            Assert.AreEqual(
                "MyVsOwnerGradeMain",
                main.TrainerName);

            CollectionAssert.AreEqual(
                new[] { 2, 2, 1 },
                aggregation.Sizes.ToArray());

            CollectionAssert.AreEqual(
                new[] { "Grade" },
                aggregation.OutputNames.ToArray());

            Assert.AreEqual(
                "MyVsOwnerGradeBase",
                aggregation.TrainerName);

            Assert.AreEqual(main.OutputSize, aggregation.InputSize);
        }

        [TestMethod]
        public void Parse_PatternVariant_PreservesOutputOrder()
        {
            var topology = TopologyParser.Parse(
                "{324,128,64,6}" +
                "[my1,my2,my3,own1,own2,own3](PatternGradeMain)" +
                "->{6,1}[Grade](PatternGradeBase)");

            CollectionAssert.AreEqual(
                new[] { "my1", "my2", "my3", "own1", "own2", "own3" },
                topology.Stages[0].OutputNames.ToArray());

            // Вход и выход, без скрытого слоя.
            CollectionAssert.AreEqual(
                new[] { 6, 1 },
                topology.Stages[1].Sizes.ToArray());
        }

        [TestMethod]
        public void Parse_WhitespaceAroundElements_IsAllowed()
        {
            var topology = TopologyParser.Parse(
                "  { 324, 128, 64, 2 } [ My, Opp ] " +
                "( MyVsOwnerGradeMain )  ->  " +
                "{ 2, 2, 1 } [ Grade ] ( MyVsOwnerGradeBase )  ");

            Assert.AreEqual(2, topology.Stages.Count);
            Assert.AreEqual("My", topology.Stages[0].OutputNames[0]);
            Assert.AreEqual("Opp", topology.Stages[0].OutputNames[1]);
            Assert.AreEqual(
                "MyVsOwnerGradeBase",
                topology.Stages[1].TrainerName);
        }

        [TestMethod]
        public void Parse_ThreeNetworks_IsAllowed()
        {
            var topology = TopologyParser.Parse(
                "{324,64,2}[My,Opp](MainTrainer)" +
                "->{2,8,3}[A,B,C](IntermediateTrainer)" +
                "->{3,1}[Grade](FinalTrainer)");

            Assert.AreEqual(3, topology.Stages.Count);
            Assert.AreEqual(3, topology.Stages[1].OutputSize);
            Assert.AreEqual(3, topology.Stages[2].InputSize);
            Assert.AreEqual(1, topology.Stages[2].OutputSize);
        }

        [TestMethod]
        public void Parse_DifferentAdjacentSizes_DefersEncoderValidation()
        {
            var topology = TopologyParser.Parse(
                "{324,2}[My,Opp](MainTrainer)" +
                "->{6,1}[Grade](ExpandingEncoderTrainer)");

            // Это лишь синтаксически корректное описание.
            // Допустимость перехода 2 -> кодировщик -> 6
            // проверяется при сборке каскада.
            Assert.AreEqual(2, topology.Stages[0].OutputSize);
            Assert.AreEqual(6, topology.Stages[1].InputSize);
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("{324}[Grade](GradeMain)")]
        [DataRow("{0,1}[Grade](GradeMain)")]
        [DataRow("{324,-1,1}[Grade](GradeMain)")]
        [DataRow("{324,+1}[Grade](GradeMain)")]
        [DataRow("{324,1.5,1}[Grade](GradeMain)")]
        [DataRow("{324,abc,1}[Grade](GradeMain)")]
        [DataRow("{324,2147483648,1}[Grade](GradeMain)")]
        [DataRow("{324,,1}[Grade](GradeMain)")]
        [DataRow("{324,1,}[Grade](GradeMain)")]
        [DataRow("{324-128-64-1}[Grade](GradeMain)")]
        [DataRow("{324,1}[](GradeMain)")]
        [DataRow("{324,1}[Grade,Other](GradeMain)")]
        [DataRow("{324,1}[Grade]()")]
        [DataRow("{324,1}[Grade]")]
        [DataRow("{324,1}(GradeMain)")]
        [DataRow("{324,1}[Grade](GradeMain) extra")]
        [DataRow("->{324,1}[Grade](GradeMain)")]
        [DataRow("{324,1}[Grade](GradeMain)->")]
        [DataRow("{324,1}[Grade](GradeMain)->->{1,1}[Grade](Next)")]
        [DataRow("{324,2}[My,Opp](MainTrainer)")]
        [DataRow("{324,2}[My,My](Main)->{2,1}[Grade](Base)")]
        [DataRow("{324,2}[My, ](Main)->{2,1}[Grade](Base)")]
        [DataRow("{324,1}[Grade](Оценка (позиции))")]
        [DataRow("{324,1}[Grade](Оценка[позиции])")]
        [DataRow("{324,1}[Grade](Оценка{позиции})")]
        [DataRow("{324,1}[Grade](Оценка->позиции)")]
        public void Parse_InvalidDescription_ThrowsFormatException(
            string value)
        {
            Assert.ThrowsException<FormatException>(() =>
            {
                TopologyParser.Parse(value);
            });
        }

        [DataTestMethod]
        [DataRow("Оценка позиции")]
        [DataRow("Свои и чужие")]
        [DataRow("Мой обработчик №1")]
        [DataRow("123Trainer")]
        [DataRow("Оценка-v2")]
        public void Parse_HumanReadableTrainerName_IsAllowed(string name)
        {
            var topology = TopologyParser.Parse(
                $"{{324,1}}[Grade]({name})");

            Assert.AreEqual(name, topology.Stages[0].TrainerName);
        }

        [TestMethod]
        public void Parse_TrainerName_TrimsOuterWhitespace()
        {
            var topology = TopologyParser.Parse(
                "{324,1}[Grade](  Оценка позиции  )");

            Assert.AreEqual(
                "Оценка позиции",
                topology.Stages[0].TrainerName);
        }

        [TestMethod]
        public void Parse_WhitespaceOnlyTrainerName_ThrowsFormatException()
        {
            Assert.ThrowsException<FormatException>(() =>
            {
                TopologyParser.Parse("{324,1}[Grade](   )");
            });
        }

        [TestMethod]
        public void Parse_InvalidSecondStage_ReportsItsNumber()
        {
            var exception = Assert.ThrowsException<FormatException>(() =>
            {
                TopologyParser.Parse(
                    "{324,2}[My,Opp](MainTrainer)" +
                    "->{2,0,1}[Grade](BaseTrainer)");
            });

            StringAssert.Contains(exception.Message, "сеть №2");
        }

        [TestMethod]
        public void Parse_UnknownTrainerName_PreservesName()
        {
            var topology = TopologyParser.Parse(
                "{324,1}[Grade](Будущий обработчик)");

            // Наличие имени в реестре проверяется при сборке каскада.
            Assert.AreEqual(
                "Будущий обработчик",
                topology.Stages[0].TrainerName);
        }
    }
}