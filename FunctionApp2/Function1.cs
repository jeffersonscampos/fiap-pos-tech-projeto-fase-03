using Azure.Messaging.ServiceBus;
using FunctionApp2.Objetos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static System.Net.Mime.MediaTypeNames;

namespace FunctionApp2
{
    public class Function1
    {
        private readonly ILogger<Function1> _logger;

        public Function1(ILogger<Function1> logger)
        {
            _logger = logger;
        }

        private string connectionString = "Server=tcp:fiapservergroup.database.windows.net,1433;Initial Catalog=FiapDataBaseGroup;Persist Security Info=False;User ID=FiapPosTechUser;Password=@Fiap2024;";

        [Function(nameof(Function1))]
        public async Task Run(
            [ServiceBusTrigger("aprovacaodecompras", Connection = "ServiceBusConnection")]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions)
        {
            _logger.LogInformation("Message ID: {id}", message.MessageId);
            _logger.LogInformation("Message Body: {body}", message.Body);
            _logger.LogInformation("Message Content-Type: {contentType}", message.ContentType);

            //var primeira = Encoding.UTF8.GetString(message.Body);

            //var teste = JsonConvert.DeserializeObject<Dictionary<string, string>>(message.Body.ToString());

            var teste = message.Body.ToString();
            var xpto = JObject.Parse(teste);
            var outra = xpto["message"]  as JObject;
            ComprarAcoesServiceBus? compra =  outra?.ToObject<ComprarAcoesServiceBus>();


            if (compra.Carteira.Saldo > 0 && compra.Carteira.Saldo >= compra.Quantidade * compra.Acao.Valor)
            {
                var valorCompra = compra.Quantidade * compra.Acao.Valor;
                DeduzirSaldoCarteira(compra.Carteira.Id, valorCompra);
            }

            //regras:
            // verificar se possui saldo
            // verificar se quantidade* preco menor que saldo
            // 
            // deduzir o saldo da carteira
            // update Carteira set Saldo -= @quantidade * @valor where idCarteira = @idCarteira

            //verificar se a acao está na lista de ativos da carteira


            // update Ativos set quantidade += @quantidade, dataCompra = getdate() where id = @id
            // 
            // insert into Ativos(idCateira, quantidade, dataCompra, acaoId) values(@idCarteira, @quantidade, getdate(), @acaoId)


            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string sql = "SELECT * FROM Usuario;";
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    connection.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // Processar os resultados aqui
                            var nome = reader["Nome"];

                            _logger.LogInformation("Nome: {nome}", nome);
                        }
                    }
                }
            }

            // Complete the message
            //await messageActions.CompleteMessageAsync(message);
        }

        private void DeduzirSaldoCarteira(int idCarteira, float valorCompra)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string sql = "update Carteira set Saldo -= @quantidade * @valor where idCarteira = @idCarteira;";
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    try
                    {
                        connection.Open();
                        command.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }
                }
            }

            /*
             public void InsertExample()
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string sql = "INSERT INTO SuaTabela (Coluna1, Coluna2) VALUES (@Value1, @Value2);";
                    using (SqlCommand command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@Value1", valor1);
                        command.Parameters.AddWithValue("@Value2", valor2);
                        connection.Open();
                        command.ExecuteNonQuery();
                    }
                }
            }

            public void UpdateExample()
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string sql = "UPDATE SuaTabela SET Coluna1 = @NovoValor WHERE Coluna2 = @ValorAntigo;";
                    using (SqlCommand command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@NovoValor", novoValor);
                        command.Parameters.AddWithValue("@ValorAntigo", valorAntigo);
                        connection.Open();
                        command.ExecuteNonQuery();
                    }
                }
            }
             */
        }
    }
}
