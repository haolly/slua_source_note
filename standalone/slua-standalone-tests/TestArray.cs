using System;
using NUnit.Framework;

namespace SLua.Test
{
    [TestFixture]
    public class TestArray
    {
        private LuaSvr luaSvr;

        [SetUp]
        public void Init()
        {
            luaSvr = new LuaSvr();
            luaSvr.init(x => { }, () => { });
        }

        /// <summary>
        /// Slua.MakeArray will trigger C# function call, which push an array into stack(and cache it)
        /// luaState.doString call checkVar to get the cache object in c#
        /// </summary>
        [Test]
        public void TestCreateArray()
        {
            var code = @"
    local arr = Slua.MakeArray('System.UInt64', {1,2,3})
    return arr
";
            var ret = luaSvr.luaState.doString(code);
            var t = ret.GetType();
            Assert.AreEqual(typeof(ulong[]), ret.GetType());
        }

        /// <summary>
        /// arr[1] will trigger c# LuaArray __newindex function, which change the cached object
        /// </summary>
        [Test]
        public void ArraryIndexSet()
        {
            var code = @"
function Func(arr)
    arr[1] = 123
end
return Func
";
            var ret = luaSvr.luaState.doString(code);
            Assert.AreEqual(typeof (LuaFunction), ret.GetType());
            var func = ret as LuaFunction;
            var arr = new object[10];
            func.call((object)arr);
            Assert.AreEqual(123, arr[0]);
            var arr2 = new long[10];
            func.call((object)arr2);
            Assert.AreEqual(123, arr[0]);

        }
        [Test]
        public void ArrayAssign()
        {
            var code = @"

    local arr = Slua.MakeArray('System.Object', {'a', 1, {1}})
    return arr
";
            var ret = luaSvr.luaState.doString(code);
            Assert.AreEqual(typeof (object[]), ret.GetType());
            var arr = (object[]) ret;
            Assert.AreEqual(3, arr.Length);
        }
    }
}
