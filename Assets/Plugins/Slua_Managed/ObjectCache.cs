// The MIT License (MIT)

// Copyright 2015 Siney/Pangweiwei siney@yeah.net
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System.Runtime.CompilerServices;

namespace SLua
{
	using System;
	using System.Runtime.InteropServices;
	using System.Collections.Generic;
	using System.Runtime.CompilerServices;

    /// <summary>
    /// Each IntPtr(ie, state) has a cache
	/// each cache is a list(or dictionary), which is equallity a stack
    /// </summary>
	public class ObjectCache
	{
        /// <summary>
        /// TODO: why need map pointer to cache list? to prevent GC ??
        /// </summary>
        /// <returns></returns>
		static Dictionary<IntPtr, ObjectCache> multiState = new Dictionary<IntPtr, ObjectCache>();

		static IntPtr oldl = IntPtr.Zero;
		static internal ObjectCache oldoc = null;

        /// <summary>
        /// 從緩存中查找 **l** 對應的緩存字典，如果沒有，就從__main_state 所對應的緩存中去找
		/// 如果还没有，返回null
        /// </summary>
        /// <param name="l"></param>
        /// <returns></returns>
		public static ObjectCache get(IntPtr l)
		{
			if (oldl == l)
				return oldoc;
			ObjectCache oc;
			if (multiState.TryGetValue(l, out oc))
			{
				oldl = l;
				oldoc = oc;
				return oc;
			}

			LuaDLL.lua_getglobal(l, "__main_state");
			if (LuaDLL.lua_isnil(l, -1))
			{
				LuaDLL.lua_pop(l, 1);
				return null;
			}

			IntPtr nl = LuaDLL.lua_touserdata(l, -1);
			LuaDLL.lua_pop(l, 1);
			if (nl != l)
				return get(nl);
			return null;
		}

		class ObjSlot
		{
			public int freeslot;
			public object v;
			public ObjSlot(int slot, object o)
			{
				freeslot = slot;
				v = o;
			}
		}

#if SPEED_FREELIST
		class FreeList : List<ObjSlot>
		{
			public FreeList()
			{
				this.Add(new ObjSlot(0, null));
			}

			public int add(object o)
			{
				ObjSlot free = this[0];
				if (free.freeslot == 0)
				{
					Add(new ObjSlot(this.Count, o));
					return this.Count - 1;
				}
				else
				{
					int slot = free.freeslot;
					free.freeslot = this[slot].freeslot;
					this[slot].v = o;
					this[slot].freeslot = slot;
					return slot;
				}
			}

			public void del(int i)
			{
				ObjSlot free = this[0];
				this[i].freeslot = free.freeslot;
				this[i].v = null;
				free.freeslot = i;
			}

			public bool get(int i, out object o)
			{
				if (i < 1 || i > this.Count)
				{
					throw new ArgumentOutOfRangeException();
				}

				ObjSlot slot = this[i];
				o = slot.v;
				return o != null;
			}

			public object get(int i)
			{
				object o;
				if (get(i, out o))
					return o;
				return null;
			}

			public void set(int i, object o)
			{
				this[i].v = o;
			}
		}
#else

		class FreeList : Dictionary<int, object>
		{
			private int id = 1;
			public int add(object o)
			{
				Add(id, o);
				return id++;
			}

			public void del(int i)
			{
				this.Remove(i);
			}

			public bool get(int i, out object o)
			{
				return TryGetValue(i, out o);
			}

			public object get(int i)
			{
				object o;
				if (TryGetValue(i, out o))
					return o;
				return null;
			}

			public void set(int i, object o)
			{
				this[i] = o;
			}
		}

#endif

        /// <summary>
		/// All cached object
        /// </summary>
		FreeList cache = new FreeList();

        //all cached object which can be gc collected, the value is the index at cache list
		//TODO: why use this extra map to store gc collectable object?
		Dictionary<object, int> objMap = new Dictionary<object, int>(new ObjEqualityComparer());
        /// <summary>
		///the ref corresponding to the cache table, when the object to be pushed is a GC collectable,
		/// cache the list index as a userdata to this table, TODO: why?
  		/// </summary>
		int udCacheRef = 0;
        public class ObjEqualityComparer : IEqualityComparer<object>
        {
            public new bool Equals(object x, object y)
            {

                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }

        /// <summary>
        /// create the cache corresponding to the state
        /// </summary>
        /// <param name="l"></param>
		public ObjectCache(IntPtr l)
		{
			LuaDLL.lua_newtable(l);
			LuaDLL.lua_newtable(l);
			LuaDLL.lua_pushstring(l, "v");
			LuaDLL.lua_setfield(l, -2, "__mode");
			//NOTE: must set filed __mode before set metatable
			LuaDLL.lua_setmetatable(l, -2);
			udCacheRef = LuaDLL.luaL_ref(l, LuaIndexes.LUA_REGISTRYINDEX);
		}


		static public void clear()
		{

			oldl = IntPtr.Zero;
			oldoc = null;

		}
		internal static void del(IntPtr l)
		{
			multiState.Remove(l);
		}

        /// <summary>
        /// make a ObjectCache associate with the luaState
        /// </summary>
        /// <param name="l"></param>
		internal static void make(IntPtr l)
		{
			ObjectCache oc = new ObjectCache(l);
			multiState[l] = oc;
			oldl = l;
			oldoc = oc;
		}

        public int size()
        {
            return objMap.Count;
        }

        /// <summary>
        /// TODO: ~~the index is the address of pointer, which is an unique int, where did it got set in this cache ?~~
        /// </summary>
        /// <param name="index"></param>
		internal void gc(int index)
		{
			object o;
			if (cache.get(index, out o))
			{
				int oldindex;
				if (isGcObject(o) && objMap.TryGetValue(o,out oldindex) && oldindex==index)
				{
					objMap.Remove(o);
				}
				cache.del(index);
			}
		}
#if !SLUA_STANDALONE
        internal void gc(UnityEngine.Object o)
        {
            int index;
            if(objMap.TryGetValue(o, out index))
            {
                objMap.Remove(o);
                cache.del(index);
            }
        }
#endif

		internal int add(object o)
		{
			int objIndex = cache.add(o);
			if (isGcObject(o))
			{
				objMap[o] = objIndex;
			}
			return objIndex;
		}

		/// <summary>
		/// p is the index in the cache list
		/// </summary>
		/// <param name="l"></param>
		/// <param name="p"></param>
		/// <returns></returns>
		internal object get(IntPtr l, int p)
		{
			int index = LuaDLL.luaS_rawnetobj(l, p);
			object o;
			if (index != -1 && cache.get(index, out o))
			{
				return o;
			}
			return null;

		}

		internal void setBack(IntPtr l, int p, object o)
		{

			int index = LuaDLL.luaS_rawnetobj(l, p);
			if (index != -1)
			{
				cache.set(index, o);
			}

		}

		internal void push(IntPtr l, object o)
		{
			push(l, o, true);
		}

		

        /// <summary>
        /// analogy with function push(IntPtr l, object o, bool checkReflect)
		/// TODO: 为什么要吧Array单独出来? only difference is the QAName
        /// </summary>
        /// <param name="l"></param>
        /// <param name="o"></param>
		internal void push(IntPtr l, Array o)
		{
            //real push object into list
			int index = allocID (l, o);
			if (index < 0)
				return;

			LuaDLL.luaS_pushobject(l, index, "LuaArray", true, udCacheRef);
		}

        /// <summary>
        /// push o to cache list
        /// </summary>
        /// <param name="l"></param>
        /// <param name="o"></param>
        /// <returns></returns>
		internal int allocID(IntPtr l,object o) {

			int index = -1;

			if (o == null)
			{
				LuaDLL.lua_pushnil(l);
				return index;
			}

			bool gco = isGcObject(o);
			bool found = gco && objMap.TryGetValue(o, out index);
			if (found)
			{
				if (LuaDLL.luaS_getcacheud(l, index, udCacheRef) == 1)
					return index;
			}

			index = add(o);
			return index;
		}

		internal void push(IntPtr l, object o, bool checkReflect)
		{
			
			int index = allocID (l, o);
			if (index < 0)
				return;

			bool gco = isGcObject(o);

#if SLUA_CHECK_REFLECTION
			int isReflect = LuaDLL.luaS_pushobject(l, index, getAQName(o), gco, udCacheRef);
			if (isReflect != 0 && checkReflect)
			{
				Logger.LogWarning(string.Format("{0} not exported, using reflection instead", o.ToString()));
			}
#else
			LuaDLL.luaS_pushobject(l, index, getAQName(o), gco, udCacheRef);
#endif

		}

		static Dictionary<Type, string> aqnameMap = new Dictionary<Type, string>();
		static string getAQName(object o)
		{
			Type t = o.GetType();
			return getAQName(t);
		}

		internal static string getAQName(Type t)
		{
			string name;
			if (aqnameMap.TryGetValue(t, out name))
			{
				return name;
			}
			name = t.AssemblyQualifiedName;
			aqnameMap[t] = name;
			return name;
		}


		bool isGcObject(object obj)
		{
			return obj.GetType().IsValueType == false;
		}
	}
}

