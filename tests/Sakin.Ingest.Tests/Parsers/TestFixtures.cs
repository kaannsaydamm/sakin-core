namespace Sakin.Ingest.Tests.Parsers;

public static class TestFixtures
{
    // Windows EventLog XML
    public static readonly string WindowsEventLogLoginSuccess = """
        <Event xmlns='http://schemas.microsoft.com/win/2004/08/events/event'>
          <System>
            <Provider Name='Microsoft-Windows-Security-Auditing' Guid='{54849625-5478-4994-A5BA-3E3B0328C30D}' />
            <EventID>4624</EventID>
            <Version>2</Version>
            <Level>0</Level>
            <Task>12544</Task>
            <Opcode>0</Opcode>
            <Keywords>0x8020000000000000</Keywords>
            <TimeCreated SystemTime='2024-01-15T10:31:00.000Z' />
            <EventRecordID>12345</EventRecordID>
            <Correlation />
            <Execution ProcessID='492' ThreadID='5432' />
            <Channel>Security</Channel>
            <Computer>DESKTOP-ABC123</Computer>
            <Security UserID='S-1-5-18' />
          </System>
          <EventData>
            <Data Name='TargetUserName'>admin</Data>
            <Data Name='TargetDomainName'>CONTOSO</Data>
            <Data Name='IpAddress'>192.168.1.50</Data>
            <Data Name='IpPort'>54321</Data>
            <Data Name='LogonType'>2</Data>
          </EventData>
        </Event>
        """;

    public static readonly string WindowsEventLogLoginFailed = """
        <Event xmlns='http://schemas.microsoft.com/win/2004/08/events/event'>
          <System>
            <Provider Name='Microsoft-Windows-Security-Auditing' Guid='{54849625-5478-4994-A5BA-3E3B0328C30D}' />
            <EventID>4625</EventID>
            <Version>0</Version>
            <Level>0</Level>
            <Task>12544</Task>
            <Opcode>0</Opcode>
            <Keywords>0x8010000000000000</Keywords>
            <TimeCreated SystemTime='2024-01-15T10:32:00.000Z' />
            <EventRecordID>12346</EventRecordID>
            <Correlation />
            <Execution ProcessID='492' ThreadID='5432' />
            <Channel>Security</Channel>
            <Computer>DESKTOP-XYZ789</Computer>
            <Security UserID='S-1-5-18' />
          </System>
          <EventData>
            <Data Name='UserName'>attacker</Data>
            <Data Name='Domain'>CONTOSO</Data>
            <Data Name='IpAddress'>10.0.0.100</Data>
            <Data Name='Status'>0xC000006D</Data>
            <Data Name='SubStatus'>0xC0000064</Data>
          </EventData>
        </Event>
        """;

    // Syslog messages
    public static readonly string Rfc5424SyslogMessage =
        "<190>1 2024-01-15T10:31:00.123456Z hostname tag 12345 - - Failed password for invalid user admin from 192.168.1.50 port 54321 ssh2";

    public static readonly string Rfc3164SyslogMessage =
        "Jan 15 10:31:00 hostname sudo[12345]: admin : TTY=pts/0 ; PWD=/home/admin ; USER=root ; COMMAND=/bin/bash";

    public static readonly string SshFailedLoginSyslog =
        "Jan 15 10:31:00 host sshd[12345]: Failed password for invalid user test from 203.0.113.45 port 45678 ssh2";

    // Apache Access Logs
    public static readonly string ApacheAccessLogCombined =
        "192.168.1.100 - admin [15/Jan/2024:10:31:00 +0000] \"GET /api/users HTTP/1.1\" 200 1234 \"https://example.com\" \"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36\"";

    public static readonly string ApacheAccessLogError404 =
        "10.0.0.50 - - [15/Jan/2024:10:31:01 +0000] \"POST /nonexistent HTTP/1.1\" 404 512 \"-\" \"curl/7.81.0\"";

    public static readonly string ApacheAccessLogError500 =
        "203.0.113.1 - - [15/Jan/2024:10:31:02 +0000] \"GET /admin HTTP/1.1\" 500 1024 \"-\" \"Mozilla/5.0\"";

    // Fortinet logs
    public static readonly string FortinetCefLog =
        "CEF:0|Fortinet|FortiGate|7.0.4|10000|Intrusion|5| dst=192.168.1.1 dstport=443 src=203.0.113.45 srcport=54321 proto=6 action=alert";

    public static readonly string FortinetKeyValueLog =
        "action=accept srcip=192.168.1.100 dstip=8.8.8.8 srcport=54321 dstport=53 proto=17 policyid=1 service=dns";

    public static readonly string FortinetDenyLog =
        "action=deny srcip=203.0.113.50 dstip=192.168.1.1 srcport=45678 dstport=445 proto=6 policyid=0 service=smb";
}
