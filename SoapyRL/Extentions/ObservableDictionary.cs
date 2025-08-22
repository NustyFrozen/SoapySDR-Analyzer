namespace SoapyRL.Extentions;

//https://stackoverflow.com/questions/61858644/c-sharp-add-event-to-value-inside-dictionarytkey-tvalue
public class ObservableDictionary<TKey, TValue> : Dictionary<TKey, TValue>
{
    public delegate void CustomHandler(object sender, KeyOfChangedValueEventArgs e);

    public new TValue this[TKey key]
    {
        get => base[key];
        set
        {
            base[key] = value;
            OnCollectionChanged(key);
        }
    }

    public event CustomHandler CollectionChanged;

    public new void Add(TKey key, TValue value)
    {
        base.Add(key, value);
        OnCollectionChanged(key);
    }

    protected void OnCollectionChanged(TKey key)
    {
        CollectionChanged?.Invoke(this, new KeyOfChangedValueEventArgs((Configuration.SaVar)Convert.ToInt16(key)));
    }
}

public class KeyOfChangedValueEventArgs(Configuration.SaVar key) : EventArgs
{
    public Configuration.SaVar Key = key;
}