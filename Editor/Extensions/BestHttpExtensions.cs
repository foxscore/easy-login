using System;
using System.Text.RegularExpressions;
using System.Threading;
using BestHTTP;
using BestHTTP.Cookies;

namespace Foxscore.EasyLogin.Extensions
{
    public static class BestHttpExtensions
    {
        public static HTTPResponse SendAndAwait(this HTTPRequest request)
        {
            HTTPResponse response = null;
            request.Callback = (req, res) => response = res;
            request.Send();
            while (response == null)
                Thread.Sleep(1);
            return response;
        }
    }
}