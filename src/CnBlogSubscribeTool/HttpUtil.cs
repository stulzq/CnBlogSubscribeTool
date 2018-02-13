using System;
using System.IO;
using System.Net;
using System.Text;

namespace CnBlogSubscribeTool
{
    /// <summary>
    /// Simple Http Request Class
    /// .NET Framework >= 4.0
    /// Author:stulzq
    /// CreatedTime:2017-12-12 15:54:47
    /// </summary>
    public class HttpUtil
    {
        static HttpUtil()
        {
            //Set connection limit ,Default limit is 2
            ServicePointManager.DefaultConnectionLimit = 1024;
        }

        /// <summary>
        /// Default Timeout 20s
        /// </summary>
        public static int DefaultTimeout = 20000;

        /// <summary>
        /// Is Auto Redirect
        /// </summary>
        public static bool DefalutAllowAutoRedirect = true;

        /// <summary>
        /// Default Encoding
        /// </summary>
        public static Encoding DefaultEncoding = Encoding.UTF8;

        /// <summary>
        /// Default UserAgent
        /// </summary>
        public static string DefaultUserAgent =
                "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/62.0.3202.94 Safari/537.36"
            ;

        /// <summary>
        /// Default Referer
        /// </summary>
        public static string DefaultReferer = "";

        /// <summary>
        /// httpget request
        /// </summary>
        /// <param name="url">Internet Address</param>
        /// <returns>string</returns>
        public static string GetString(string url)
        {
            var stream = GetStream(url);
            string result;
            using (StreamReader sr = new StreamReader(stream))
            {
                result = sr.ReadToEnd();
            }
            return result;

        }

        /// <summary>
        /// httppost request
        /// </summary>
        /// <param name="url">Internet Address</param>
        /// <param name="postData">Post request data</param>
        /// <returns>string</returns>
        public static string PostString(string url, string postData)
        {
            var stream = PostStream(url, postData);
            string result;
            using (StreamReader sr = new StreamReader(stream))
            {
                result = sr.ReadToEnd();
            }
            return result;

        }

        /// <summary>
        /// Create Response
        /// </summary>
        /// <param name="url"></param>
        /// <param name="post">Is post Request</param>
        /// <param name="postData">Post request data</param>
        /// <returns></returns>
        public static WebResponse CreateResponse(string url, bool post, string postData = "")
        {
            var httpWebRequest = WebRequest.CreateHttp(url);
            httpWebRequest.Timeout = DefaultTimeout;
            httpWebRequest.AllowAutoRedirect = DefalutAllowAutoRedirect;
            httpWebRequest.UserAgent = DefaultUserAgent;
            httpWebRequest.Referer = DefaultReferer;
            if (post)
            {

                var data = DefaultEncoding.GetBytes(postData);
                httpWebRequest.Method = "POST";
                httpWebRequest.ContentType = "application/x-www-form-urlencoded;charset=utf-8";
                httpWebRequest.ContentLength = data.Length;
                using (var stream = httpWebRequest.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }
            }

            try
            {
                var response = httpWebRequest.GetResponse();
                return response;
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("Request error,url:{0},IsPost:{1},Data:{2},Message:{3}", url, post, postData, e.Message), e);
            }
        }

        /// <summary>
        /// http get request
        /// </summary>
        /// <param name="url"></param>
        /// <returns>Response Stream</returns>
        public static Stream GetStream(string url)
        {
            var stream = CreateResponse(url, false).GetResponseStream();
            if (stream == null)
            {

                throw new Exception("Response error,the response stream is null");
            }
            else
            {
                return stream;

            }
        }

        /// <summary>
        /// http post request
        /// </summary>
        /// <param name="url"></param>
        /// <param name="postData">post data</param>
        /// <returns>Response Stream</returns>
        public static Stream PostStream(string url, string postData)
        {
            var stream = CreateResponse(url, true, postData).GetResponseStream();
            if (stream == null)
            {

                throw new Exception("Response error,the response stream is null");
            }
            else
            {
                return stream;

            }
        }


    }
}