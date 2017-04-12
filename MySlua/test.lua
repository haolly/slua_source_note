local function check(ok, ...)
    assert(ok, ...)
    return ...
end
local function safeCall(csFunc)
    return function(...)
        return check(csFunc(...))
    end
end

local function testFun(...)
    print(table.unpack(...))
    return false
end


testFun({1,2,3})

print("xxxxxxxxx")
local safeTest = safeCall(testFun)
safeTest({2,3,4})
