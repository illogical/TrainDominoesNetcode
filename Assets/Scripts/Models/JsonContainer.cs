using System.Collections.Generic;
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
            Json = Newtonsoft.Json.JsonConvert.SerializeObject(station.GetDominoIdsByTracks());
        }

        public JsonContainer(List<List<int>> trackDominoIds)
        {
            Json = Newtonsoft.Json.JsonConvert.SerializeObject(trackDominoIds);
        }

        public JsonContainer(Dictionary<ulong, int> playerScores)
        {
            Json = Newtonsoft.Json.JsonConvert.SerializeObject(playerScores);
        }

        public List<List<int>> GetDeserializedTrackDominoIds()
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<List<List<int>>>(Json);
        }

        public Dictionary<ulong, int> GetDeserializedPlayerScores()
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<ulong, int>>(Json);
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
