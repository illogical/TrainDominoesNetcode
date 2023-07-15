using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;

namespace Assets.Scripts.Models
{
    public class JsonContainer : INetworkSerializable
    {
        public string Json;

        public JsonContainer()
        {
            Json = string.Empty;
        }

        public JsonContainer(Station station)
        {
            Json = Newtonsoft.Json.JsonConvert.SerializeObject(station.GetDominoIdByTracks());
        }

        public JsonContainer(List<List<int>> trackDominoIds)
        {
            Json = Newtonsoft.Json.JsonConvert.SerializeObject(trackDominoIds);
        }

        public List<List<int>> GetDeserializedTrackDominoIds()
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<List<List<int>>>(Json);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            if (serializer.IsWriter)
            {
                serializer.GetFastBufferWriter().WriteValueSafe(Json);
            }
            else
            {
                serializer.GetFastBufferReader().ReadValueSafe(out Json);
            }
        }
    }
}
