using System.Collections.Generic;

namespace CnBlogSubscribeTool.Config
{
    public class MailConfig
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public string Password { get; set; }

        public List<string> ReceiveList { get; set; }
    }
}