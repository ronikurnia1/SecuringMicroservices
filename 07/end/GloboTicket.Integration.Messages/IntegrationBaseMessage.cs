using System;

namespace GloboTicket.Integration.Messages
{
    public class IntegrationBaseMessage
    {
        public Guid Id { get; set; }
        public DateTime CreationDateTime { get; set; }
        public SecurityContext SecurityContext { get; set; } = new SecurityContext();
    }


    public class SecurityContext
    {
        public string AccessToken { get; set; }
    }

}
