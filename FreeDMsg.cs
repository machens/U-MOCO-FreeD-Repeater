using System;
using System.Collections.Generic;
using System.Text;

namespace U_MOCO_FreeD_Repeater
{
    class FreeDMsg
    {
        public struct FreeDData
        {
            public double Pan;
            public double Tilt;
            public double Roll;
            public double XPos;
            public double YPos;
            public double ZPos;
            public int Zoom;
            public int Focus;
            public byte CameraId;
            public bool IsValid;
        }
        /// <summary>
        /// 解析接收到的 FreeD 数据包（29字节，类型 0xD1）
        /// </summary>
        /// <param name="data">接收到的原始字节数组</param>
        /// <returns>解析后的 FreeDData，IsValid=false 表示数据无效</returns>
        public static FreeDData ParseFreeD(byte[] data)
        {
            var result = new FreeDData { IsValid = false };

            if (data == null || data.Length < 29)
                return result;

            // 验证包头
            if (data[0] != 0xD1)
                return result;

            // 验证校验和
            if (!VerifyChecksum(data))
                return result;

            result.CameraId = data[1];

            // 解析 Pan (bytes 2,3,4) -> 大端 24bit 有符号整数
            result.Pan = ParseDegree(data[2], data[3], data[4]);
            // 解析 Tilt (bytes 5,6,7)
            result.Tilt = ParseDegree(data[5], data[6], data[7]);
            // 解析 Roll (bytes 8,9,10)
            result.Roll = ParseDegree(data[8], data[9], data[10]);
            // 解析 X 位置 (bytes 11,12,13)
            result.XPos = ParsePosition(data[11], data[12], data[13]);
            // 解析 Y 位置 (bytes 14,15,16)
            result.YPos = ParsePosition(data[14], data[15], data[16]);
            // 解析 Z 位置 (bytes 17,18,19)
            result.ZPos = ParsePosition(data[17], data[18], data[19]);
            // 解析 Zoom (bytes 20,21,22)
            result.Zoom = ParseFZ(data[20], data[21], data[22]);
            // 解析 Focus (bytes 23,24,25)
            result.Focus = ParseFZ(data[23], data[24], data[25]);

            result.IsValid = true;
            return result;
        }
        /// <summary>
        /// 验证 FreeD 数据包校验和
        /// </summary>
        public static bool VerifyChecksum(byte[] data)
        {
            if (data == null || data.Length < 29)
                return false;

            byte checkSum = 0x40;
            for (int i = 0; i < 28; i++)
                checkSum -= data[i];

            return checkSum == data[28];
        }
        /// <summary>
        /// 解析角度：大端序 3字节 -> 有符号整数 -> 除以 32768 还原为度
        /// </summary>
        private static double ParseDegree(byte high, byte mid, byte low)
        {
            // 还原编码时的字节顺序：result[n]=byte[2], result[n+1]=byte[1], result[n+2]=byte[0]
            // 所以 high=byte[2], mid=byte[1], low=byte[0]
            byte[] buf = new byte[4];
            buf[0] = low;
            buf[1] = mid;
            buf[2] = high;
            // 符号扩展：若 bit23 为1，则 byte[3]=0xFF，否则为 0x00
            buf[3] = (high & 0x80) != 0 ? (byte)0xFF : (byte)0x00;
            int value = BitConverter.ToInt32(buf, 0);
            return value / 32768.0;
        }

        /// <summary>
        /// 解析位置：大端序 3字节 -> 有符号整数 -> 除以 64 还原为单位值
        /// </summary>
        private static double ParsePosition(byte high, byte mid, byte low)
        {
            byte[] buf = new byte[4];
            buf[0] = low;
            buf[1] = mid;
            buf[2] = high;
            buf[3] = (high & 0x80) != 0 ? (byte)0xFF : (byte)0x00;
            int value = BitConverter.ToInt32(buf, 0);
            return value / 64.0;
        }

        /// <summary>
        /// 解析 Zoom / Focus：3字节小端序 -> 整数
        /// </summary>
        private static int ParseFZ(byte high, byte mid, byte low)
        {
            // 编码时：result[0]=valueByte[0], result[1]=valueByte[1], result[2]=valueByte[2]
            // 存入时：result[n]=byte[2], result[n+1]=byte[1], result[n+2]=byte[0]
            // 所以 high=valueByte[2], mid=valueByte[1], low=valueByte[0]
            byte[] buf = new byte[4];
            buf[0] = low;
            buf[1] = mid;
            buf[2] = high;
            buf[3] = 0x00;
            return BitConverter.ToInt32(buf, 0);
        }

        public static byte[] FreeDEncoder(double xPos, double yPos, double zPos, double pan, double tilt, double roll, int zoom, int focus)
        {
            byte[] result = new byte[29] { 0xD1, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x70 };

            byte[] panByte = CamerDegreeConvert(pan);
            result[2] = panByte[2];
            result[3] = panByte[1];
            result[4] = panByte[0];
            byte[] tiltByte = CamerDegreeConvert(tilt);
            result[5] = tiltByte[2];
            result[6] = tiltByte[1];
            result[7] = tiltByte[0];
            byte[] rollByte = CamerDegreeConvert(roll);
            result[8] = rollByte[2];
            result[9] = rollByte[1];
            result[10] = rollByte[0];
            byte[] xByte = CamerPositionConvert(xPos);
            result[11] = xByte[2];
            result[12] = xByte[1];
            result[13] = xByte[0];
            byte[] yByte = CamerPositionConvert(yPos);
            result[14] = yByte[2];
            result[15] = yByte[1];
            result[16] = yByte[0];
            byte[] zByte = CamerPositionConvert(zPos);
            result[17] = zByte[2];
            result[18] = zByte[1];
            result[19] = zByte[0];
            //zoom
            byte[] zoomByte = CamerFZConvert(zoom);
            result[20] = zoomByte[2];
            result[21] = zoomByte[1];
            result[22] = zoomByte[0];
            //focus
            byte[] focusByte = CamerFZConvert(focus);
            result[23] = focusByte[2];
            result[24] = focusByte[1];
            result[25] = focusByte[0];

            result = CalculatChecksum(result);

            return result;
        }

        private static byte[] CamerFZConvert(int value)
        {
            byte[] valueByte = BitConverter.GetBytes(value);
            byte[] result = new byte[3];
            result[0] = valueByte[0];
            result[1] = valueByte[1];
            result[2] = valueByte[2];
            return result;
        }
        private static byte[] CamerDegreeConvert(double degreeDouble)
        {
            byte[] degreeByte;
            int degreeInt = Convert.ToInt32(degreeDouble * 32768);

            degreeByte = BitConverter.GetBytes(degreeInt);
            byte[] result = new byte[3];
            result[0] = degreeByte[0];
            result[1] = degreeByte[1];
            result[2] = degreeByte[2];
            return result;
        }

        private static byte[] CamerPositionConvert(double positionDouble)
        {
            byte[] positionByte;
            int positionInt = Convert.ToInt32(positionDouble * 64);
            positionByte = BitConverter.GetBytes(positionInt);
            byte[] result = new byte[3];
            result[0] = positionByte[0];
            result[1] = positionByte[1];
            result[2] = positionByte[2];
            return result;
        }

        private static byte[] CalculatChecksum(byte[] orgData)
        {
            byte[] result = new byte[29];
            result = orgData;

            byte checkSum = 0x40;

            for (int i = 0; i < 28; i++)
            {
                checkSum -= orgData[i];
            }
            result[28] = checkSum;

            return result;
        }
    }
}
