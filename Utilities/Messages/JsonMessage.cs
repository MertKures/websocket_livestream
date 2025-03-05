using System.Text;
using System.Text.Json;

public class JsonMessage : Dictionary<string, object>
{
    public JsonMessage()
    {
    }

    public async Task<Result> LoadJSONAsync(byte[] jsonData, int offset, int length)
    {
        byte[] bytes = new byte[length];

        Array.Copy(jsonData, offset, bytes, 0, length);

        using (MemoryStream memoryStream = new(bytes))
        {
            try
            {
                JsonMessage? message = await JsonSerializer.DeserializeAsync<JsonMessage>(memoryStream);

                if (message == null)
                    return new ErrorResult("JSON verisi dönüştürülemedi.");

                this.Clear();

                foreach (KeyValuePair<string, object> pair in message)
                {
                    this.Add(pair.Key, pair.Value);
                }

                return new SuccessResult();
            }
            catch (Exception ex)
            {
                return new ErrorResult(ex.Message);
            }
        }
    }

    public async Task<DataResult<byte[]>> ToJSONAsync()
    {
        if (Count == 0)
            return new ErrorDataResult<byte[]>("İçerisinde hiçbir değer bulunamadığından mesaj JSON'a çevrilemedi.");

        using MemoryStream memoryStream = new();

        try
        {
            await JsonSerializer.SerializeAsync<Dictionary<string, object>>(
                memoryStream,
                this
            );
            return new SuccessDataResult<byte[]>(memoryStream.ToArray());
        }
        catch (Exception ex)
        {
            return new ErrorDataResult<byte[]>(ex.Message);
        }
    }

    public override string ToString()
    {
        StringBuilder sb = new();

        if (Count == 0)
            return "{}";

        sb.Append('{').AppendLine();

        foreach (KeyValuePair<string, object> pair in this)
        {
            sb.Append("\t\"").Append(pair.Key).Append("\": ").Append(pair.Value).AppendLine();
        }

        sb.Append('}');

        return sb.ToString();
    }
}
