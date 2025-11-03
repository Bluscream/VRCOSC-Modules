// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;

namespace Bluscream;

/// <summary>
/// Exception utility functions
/// </summary>
public static class ExceptionUtils
{
    public static string GetFullMessage(Exception ex)
    {
        var messages = new List<string> { ex.Message };
        var inner = ex.InnerException;
        
        while (inner != null)
        {
            messages.Add(inner.Message);
            inner = inner.InnerException;
        }
        
        return string.Join(" -> ", messages);
    }
    
    public static IEnumerable<Exception> GetInnerExceptions(Exception ex)
    {
        var inner = ex.InnerException;
        while (inner != null)
        {
            yield return inner;
            inner = inner.InnerException;
        }
    }
}
