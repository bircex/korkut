using Korkut.Tools.Calc.Tariffs;

namespace Korkut.Tools.Unit.Tests;

public class Trh2010Tests
{
    [Theory]
    [InlineData("erkek", 0, 71.93)]
    [InlineData("kadin", 0, 78.02)]
    [InlineData("erkek", 30, 44.45)]
    [InlineData("kadin", 30, 49.00)]
    [InlineData("erkek", 60, 17.62)]
    [InlineData("kadin", 60, 20.79)]
    [InlineData("erkek", 99, 0.50)]
    [InlineData("kadin", 99, 0.50)]
    public void Bakiye_omur_yayimlanan_tabloyla_eslesir(string cinsiyet, int yas, decimal beklenen)
        => Assert.Equal(beklenen, Trh2010.BakiyeOmur(cinsiyet, yas));

    [Fact]
    public void Yas_100_son_satira_kirpilir()
        => Assert.Equal(0.50m, Trh2010.BakiyeOmur("erkek", 100));

    [Fact]
    public void Gecersiz_girdiler_null_doner()
    {
        Assert.Null(Trh2010.BakiyeOmur("erkek", -1));
        Assert.Null(Trh2010.BakiyeOmur("erkek", 101));
        Assert.Null(Trh2010.BakiyeOmur("bilinmiyor", 30));
    }

    [Fact]
    public void Kadin_iki_yazimla_da_kabul_edilir()
    {
        Assert.Equal("kadin", Trh2010.NormalizeCinsiyet("kadın"));
        Assert.Equal("kadin", Trh2010.NormalizeCinsiyet("kadin"));
        Assert.Equal("erkek", Trh2010.NormalizeCinsiyet(" Erkek "));
    }
}
