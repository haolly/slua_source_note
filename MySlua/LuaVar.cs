abstract public class LuaVar : IDisposable
{
    protected LuaState state = null;
    protected int valueref = 0;

    public IntPtr L 
    {
        get
        {
            return state.L;
        }
    }

    public int Ref
    {
        get
        {
            return valueref;
        }
    }

    public LuaVar()
    {
        state = null;
    }

    public LuaVar(LuaState l, int r)
    {
        state = l;
        valueref = r;
    }

    public LuaVar(IntPtr l, int r)
    {
        state = LuaState.get(l);
        valueref = r;
    }

    ~LuaVar()
    {
        Dispose(this);
    }

    public void Dispose()
    {
        Dispose(true)
        GC.SupressFinalize(this);
    }
}