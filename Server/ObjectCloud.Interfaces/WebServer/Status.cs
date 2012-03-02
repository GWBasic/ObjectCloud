// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

namespace ObjectCloud.Interfaces.WebServer
{
    /// <summary>
    /// All of the statuses that the web server can return.
    /// see http://en.wikipedia.org/wiki/List_of_HTTP_status_codes
    /// </summary>
    public enum Status : int
    {
        _100_Continue = 100,
        _101_Switching_Protocols = 101,
        _102_Processing = 102,
        _122_Request_URI_too_long = 122,
        
        _200_OK = 200,
        _201_Created = 201,
        _202_Accepted = 202,
        _203_Non_Authoritative_Information = 203,
        _204_No_Content = 204,
        _205_Reset_Content = 205,
        _206_Partial_Content = 206,
        _207_Multi_Status = 207,
        
        _300_Multiple_Choices = 300,
        _301_Moved_Permanently = 301,
        _302_Found = 302,
        _303_See_Other = 303,
        _304_Not_Modified = 304,
        _305_Use_Proxy = 305,
        _306_Switch_Proxy = 306,
        _307_Temporary_Redirect = 307,
        
        _400_Bad_Request = 400,
        _401_Unauthorized = 401,
        _402_Payment_Required = 402,
        _403_Forbidden = 403,
        _404_Not_Found = 404,
        _405_Method_Not_Allowed = 405,
        _406_Not_Acceptable = 406,
        _407_Proxy_Authentication_Required = 407,
        _408_Request_Timeout = 408,
        _409_Conflict = 409,
        _410_Gone = 410,
        _411_Length_Required = 411,
        _412_Precondition_Failed = 412,
        _413_Request_Entity_Too_Large = 413,
        _414_Request_URI_Too_Long = 414,
        _415_Unsupported_Media_Type = 415,
        _416_Requested_Range_Not_Satisfiable = 416,
        _417_Expectation_Failed = 417,
        _418_Im_a_teapot = 418,
        _422_Unprocessable_Entity = 422,
        _423_Locked = 423,
        _424_Failed_Dependency = 424,
        _425_Unordered_Collection = 425,
        _426_Upgrade_Required = 426,
        _449_Retry_With = 449,
        _450_Blocked = 450,

        _500_Internal_Server_Error = 500,
        _501_Not_Implemented = 501,
        _502_Bad_Gateway = 502,
        _503_Service_Unavailable = 503,
        _504_Gateway_Timeout = 504,
        _505_HTTP_Version_Not_Supported = 505,
        _506_Variant_Also_Negotiates = 506,
        _507_Insufficient_Storage = 507,
        _509_Bandwidth_Limit_Exceeded = 509,
        _510_Not_Extended = 510
    }
}