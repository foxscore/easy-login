using System;
using System.Text;

namespace Foxscore.EasyLogin.Extensions
{
    public static class StringExtensions
    {
        public static string ToBase64(this string s)
        {
            var b = Encoding.UTF8.GetBytes(s);
            return Convert.ToBase64String(b);
        }
        
        public static string FromBase64(this string s)
        {
            var b = Convert.FromBase64String(s);
            return Encoding.UTF8.GetString(b);
        }
    }
}