using System;
using System.Collections.Generic;
using Xunit;
using CSLogix.Models;

namespace CSLogix.Tests.Models
{
    public class UDTTests
    {
        [Fact]
        public void DefaultConstructor_InitializesProperties()
        {
            var udt = new UDT();

            Assert.Equal(0, udt.Type);
            Assert.Equal(string.Empty, udt.Name);
            Assert.NotNull(udt.Fields);
            Assert.Empty(udt.Fields);
            Assert.NotNull(udt.FieldsByName);
            Assert.Empty(udt.FieldsByName);
        }

        [Fact]
        public void Properties_CanBeSet()
        {
            var udt = new UDT
            {
                Type = 0x1234,
                Name = "MyCustomType"
            };

            Assert.Equal(0x1234, udt.Type);
            Assert.Equal("MyCustomType", udt.Name);
        }

        [Fact]
        public void Fields_CanAddTags()
        {
            var udt = new UDT { Name = "TestUDT" };

            var field1 = new Tag { TagName = "Field1", DataType = "DINT" };
            var field2 = new Tag { TagName = "Field2", DataType = "REAL" };

            udt.Fields.Add(field1);
            udt.Fields.Add(field2);

            Assert.Equal(2, udt.Fields.Count);
            Assert.Equal("Field1", udt.Fields[0].TagName);
            Assert.Equal("Field2", udt.Fields[1].TagName);
        }

        [Fact]
        public void FieldsByName_CanLookupByName()
        {
            var udt = new UDT { Name = "TestUDT" };

            var field1 = new Tag { TagName = "Counter", DataType = "DINT" };
            var field2 = new Tag { TagName = "Temperature", DataType = "REAL" };

            udt.FieldsByName["Counter"] = field1;
            udt.FieldsByName["Temperature"] = field2;

            Assert.Equal(2, udt.FieldsByName.Count);
            Assert.Equal("DINT", udt.FieldsByName["Counter"].DataType);
            Assert.Equal("REAL", udt.FieldsByName["Temperature"].DataType);
        }

        [Fact]
        public void ToString_ReturnsFormattedString()
        {
            var udt = new UDT
            {
                Type = 0x1000,
                Name = "MyUDT"
            };
            udt.Fields.Add(new Tag { TagName = "Field1" });
            udt.Fields.Add(new Tag { TagName = "Field2" });
            udt.Fields.Add(new Tag { TagName = "Field3" });

            string result = udt.ToString();

            Assert.Equal("UDT(Type=4096, Name=MyUDT, Fields=3)", result);
        }

        [Fact]
        public void Fields_AndFieldsByName_CanBeSynced()
        {
            var udt = new UDT { Name = "SyncedUDT", Type = 0x2000 };

            var fields = new List<Tag>
            {
                new Tag { TagName = "IntField", DataType = "DINT", DataTypeValue = 0xC4 },
                new Tag { TagName = "RealField", DataType = "REAL", DataTypeValue = 0xCA },
                new Tag { TagName = "BoolField", DataType = "BOOL", DataTypeValue = 0xC1 }
            };

            foreach (var field in fields)
            {
                udt.Fields.Add(field);
                udt.FieldsByName[field.TagName] = field;
            }

            Assert.Equal(3, udt.Fields.Count);
            Assert.Equal(3, udt.FieldsByName.Count);
            Assert.Same(udt.Fields[0], udt.FieldsByName["IntField"]);
            Assert.Same(udt.Fields[1], udt.FieldsByName["RealField"]);
            Assert.Same(udt.Fields[2], udt.FieldsByName["BoolField"]);
        }

        [Fact]
        public void NestedUDT_CanBeRepresented()
        {
            // Create inner UDT
            var innerUdt = new UDT
            {
                Type = 0x1001,
                Name = "InnerType"
            };
            innerUdt.Fields.Add(new Tag { TagName = "Value", DataType = "DINT" });

            // Create outer UDT with a field referencing inner
            var outerUdt = new UDT
            {
                Type = 0x1002,
                Name = "OuterType"
            };

            var nestedField = new Tag
            {
                TagName = "NestedData",
                DataType = "InnerType",
                Struct = 1,
                UDT = innerUdt
            };

            outerUdt.Fields.Add(nestedField);
            outerUdt.FieldsByName["NestedData"] = nestedField;

            Assert.NotNull(outerUdt.FieldsByName["NestedData"].UDT);
            Assert.Equal("InnerType", outerUdt.FieldsByName["NestedData"].UDT!.Name);
            Assert.Single(outerUdt.FieldsByName["NestedData"].UDT!.Fields);
        }
    }
}
