namespace GitSync;

public class Mapper : MapperBase
{
    public Mapper Add(Parts from, Parts to)
    {
        this.AddOrRemoveInternal(from, to);
        return this;
    }

    public Mapper Add(Parts from, params Parts[] tos)
    {
        foreach (var to in tos)
        {
            this.AddOrRemoveInternal(from, to);
        }

        return this;
    }

    public Mapper Remove(Parts to)
    {
        this.AddOrRemoveInternal(Parts.Empty, to);
        return this;
    }
}
