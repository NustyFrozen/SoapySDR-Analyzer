namespace SoapyRL.Extentions;

//https://stackoverflow.com/questions/61858644/c-sharp-add-event-to-value-inside-dictionarytkey-tvalue
public class ObservableDictionary<TKey, TValue> : Dictionary<TKey, TValue>
{
    public delegate void customHandler(object sender, keyOfChangedValueEventArgs e);

    public new TValue this[TKey key]
    {
        get => base[key];
        set
        {
            base[key] = value;
            OnCollectionChanged(key);
        }
    }

    public event customHandler CollectionChanged;

    public new void Add(TKey key, TValue value)
    {
        base.Add(key, value);
        OnCollectionChanged(key);
    }

    protected void OnCollectionChanged(TKey key)
    {
        CollectionChanged?.Invoke(this, new keyOfChangedValueEventArgs((Configuration.saVar)Convert.ToInt16(key)));
    }
}

public class keyOfChangedValueEventArgs(Configuration.saVar key) : EventArgs
{
    public Configuration.saVar key = key;
}