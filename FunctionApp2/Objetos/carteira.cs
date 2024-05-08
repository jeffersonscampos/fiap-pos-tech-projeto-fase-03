using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FunctionApp2.Objetos
{
    public class Comum 
    {
        public int Id { get; set; }
    }
    public class ComprarAcoesServiceBus
    {
        public int Quantidade { get; set; }
        public Carteira Carteira { get; set; }
        public Acao Acao { get; set; }
    }

    public class Acao : Comum
    {
        public string Nome { get; set; }
        public float Valor { get; set; }

        public Acao() { }
    }

    public class Carteira : Comum
    {
        public int UsuarioId { get; set; }
        public float Saldo { get; set; }
        public List<Ativos>? Acoes { get; set; }

        public Usuario Usuario { get; set; }

        public Carteira()
        {
            Acoes = new List<Ativos>();
        }
        public Carteira(int id, int usuarioId, float saldo)
        {
            Id = id;
            UsuarioId = usuarioId;
            Saldo = saldo;

        }
    }

    public class Ativos : Comum
    {
        public int IdCarteira { get; set; }
        public int Quantidade { get; set; }
        public DateTime DataCompra { get; set; }
        public Acao Acao { get; set; }
        public Carteira Carteira { get; set; }

        public Ativos()
        {
            Acao = new Acao();
        }
    }
    public class Usuario : Comum
    {
        public string Nome { get; set; }
        public string Senha { get; set; }
        public TipoPermissao Permissao { get; set; }
        public Carteira Carteira { get; set; }

        public Usuario() { }
    }

    public enum TipoPermissao 
    {
        Usuario = 1, Administrador = 2
    }
}
