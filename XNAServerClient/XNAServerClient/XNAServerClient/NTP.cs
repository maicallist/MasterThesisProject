using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace XNAServerClient
{
    class NTP
    {
        public static DateTime GetNetworkTime()
        { 
            //ntp server addr
            const String ntpServer = "time.windows.com";
            //ntp message size - 16 byte of the digest (RFC 2030)
            /*
             0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            |LI | VN  |Mode |    Stratum    |     Poll      |   Precision   |
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            |                          Root Delay                           |
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            |                       Root Dispersion                         |
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            |                     Reference Identifier                      |
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            |                                                               |
            |                   Reference Timestamp (64)                    |
            |                   time local clock was last corrected         |
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            |                                                               |
            |                   Originate Timestamp (64)                    |
            |                   time at which request departed the client   |
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            |                                                               |
            |                    Receive Timestamp (64)                     |
            |                    time at which request arrived at server    |
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            |                                                               |
            |                    Transmit Timestamp (64)                    |
            |                    time at which reply departed the server    |
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            |                 Key Identifier (optional) (32)                |
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            |                                                               |
            |                                                               |
            |                 Message Digest (optional) (128)               |
            |                                                               |
            |                                                               |
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            */

            /*
             * 
             */
            var ntpData = new byte[48];
            //Setting the Leap Indicator, Version Number and Model Values
            ntpData[0] = 0x1B; //LI = 0, VN = 3 (IPv4 Only), Mode = 3 (Client Mode)

            var addresses = Dns.GetHostEntry(ntpServer).AddressList;

            //assign udp port to ntp
            var ipEndPoint = new IPEndPoint(addresses[0], 123);
            //ntp uses udp
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            socket.Connect(ipEndPoint);

            //if ntp blocked
            socket.ReceiveTimeout = 3000;

            socket.Send(ntpData);
            socket.Receive(ntpData);
            socket.Close();

            //Offset to get to the "Transmit Timestamp" field (time at which the reply
            //departed the server for client, in 64-bit timestamp format).

            //see data format above, each line represent 32 bits
            //Transmit Timestamp starts from 320th bits to 383th bits
            const byte serverReplyTime = 40;

            /* 
             * ulong 
             * unsigned 
             * 0 to 18,446,744,073,709,551,615 
             * 
             * long
             * signed
             * –9,223,372,036,854,775,808 to 9,223,372,036,854,775,807
             */

            //bitconvert : Returns a 32-bit unsigned integer 
            //converted from four bytes at a specified position in a byte array
            
            //get the second part (from index 40)
            ulong intPart = BitConverter.ToUInt32(ntpData, serverReplyTime);

            //get the second fraction (4 * 8 bits = 32)
            ulong fractPart = BitConverter.ToUInt32(ntpData, serverReplyTime + 4);

            //convert from big-endian to little-endian
            intPart = SwapEndianness(intPart);
            fractPart = SwapEndianness(fractPart);

            var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);

            //**UTC** time
            var networkDateTime = (new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)).AddMilliseconds((long)milliseconds);

            return networkDateTime.ToLocalTime();
        }

        public static uint SwapEndianness(ulong x)
        {
            return (uint)(((x & 0x000000ff) << 24) +
                           ((x & 0x0000ff00) << 8) +
                           ((x & 0x00ff0000) >> 8) +
                           ((x & 0xff000000) >> 24));
        }

        public static long ElapsedTicks(DateTime time)
        {
            DateTime begin = new DateTime(2015, 7, 1);

            long elapsedTicks = time.Ticks - begin.Ticks;

            return elapsedTicks;
        }
    }
}
