// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Jint;
using Jint.Native;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Game.API.Scripting.Api;

namespace osu.Game.Tests.API.Scripting
{
    [TestFixture]
    public class ScriptApiBinderTest
    {
        private Engine engine = null!;

        [SetUp]
        public void SetUp()
        {
            engine = new Engine();
        }

        [Test]
        public void TestConvertStringArgument()
        {
            JsValue value = "hello";
            ClassicAssert.AreEqual("hello", ScriptApiBinder.ConvertArgument(value, typeof(string)));
        }

        [Test]
        public void TestConvertNonStringArgumentReturnsNull()
        {
            JsValue value = 42;
            ClassicAssert.IsNull(ScriptApiBinder.ConvertArgument(value, typeof(string)));
        }

        [Test]
        public void TestConvertNumberArgument()
        {
            JsValue value = 3.5;
            ClassicAssert.AreEqual(3.5, ScriptApiBinder.ConvertArgument(value, typeof(double)));
            ClassicAssert.AreEqual(3, ScriptApiBinder.ConvertArgument(value, typeof(int)));
        }

        [Test]
        public void TestConvertBooleanArgument()
        {
            JsValue value = true;
            ClassicAssert.AreEqual(true, ScriptApiBinder.ConvertArgument(value, typeof(bool)));
        }

        [Test]
        public void TestConvertJsValuePassesThrough()
        {
            JsValue value = "hello";
            ClassicAssert.AreSame(value, ScriptApiBinder.ConvertArgument(value, typeof(JsValue)));
        }

        [Test]
        public void TestConvertResultString()
        {
            ClassicAssert.AreEqual("hello", ScriptApiBinder.ConvertResult(engine, typeof(string), "hello").AsString());
        }

        [Test]
        public void TestConvertResultVoidIsUndefined()
        {
            ClassicAssert.IsTrue(ScriptApiBinder.ConvertResult(engine, typeof(void), null).IsUndefined());
        }

        [Test]
        public void TestConvertResultNullIsUndefined()
        {
            ClassicAssert.IsTrue(ScriptApiBinder.ConvertResult(engine, typeof(string), null).IsUndefined());
        }

        [Test]
        public void TestConvertResultBoolean()
        {
            ClassicAssert.AreEqual(true, ScriptApiBinder.ConvertResult(engine, typeof(bool), true).AsBoolean());
        }

        [Test]
        public void TestConvertResultJsValuePassesThrough()
        {
            JsValue value = "x";
            ClassicAssert.AreSame(value, ScriptApiBinder.ConvertResult(engine, typeof(JsValue), value));
        }
    }
}
