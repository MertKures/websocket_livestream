using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class ErrorResult : Result
{
    public ErrorResult(string message) : base(success: false, message)
    {

    }

    public ErrorResult() : base(success: false)
    {
    }
}
