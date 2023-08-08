using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Assets.Scripts.Models
{
    public class NetworkSerializer<T> where T : class
    {
        public byte[] Serialize(T obj)
        {
            if (obj == null)
                return null;

            BinaryFormatter bf = new BinaryFormatter();
            using (MemoryStream ms = new MemoryStream())
            {
                bf.Serialize(ms, obj);
                return ms.ToArray();
            }
        }

        public T Deserialize(byte[] arrBytes)
        {
            if (arrBytes == null)
                return null;

            BinaryFormatter bf = new BinaryFormatter();
            using (MemoryStream ms = new MemoryStream(arrBytes))
            {
                return (T)bf.Deserialize(ms);
            }
        }
    }
}