using Azure.Messaging.ServiceBus;
using FunctionApp2.Objetos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Data.SqlClient;
using System.IO.Pipelines;

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
            try
            {
                _logger.LogInformation("Message ID: {id}", message.MessageId);
                _logger.LogInformation("Message Body: {body}", message.Body);
                _logger.LogInformation("Message Content-Type: {contentType}", message.ContentType);

                var conteudoBruto = message.Body.ToString();
                var conteudoParseado = JObject.Parse(conteudoBruto);
                var mensagem = conteudoParseado["message"] as JObject;

                if (mensagem != null)
                {

                    ComprarAcoesServiceBus? compra = mensagem?.ToObject<ComprarAcoesServiceBus>();

                    if (compra != null)
                    {

                        // verificar se possui saldo e se quantidade* preco menor que saldo
                        if (compra.Carteira.Saldo > 0 && compra.Carteira.Saldo >= compra.Quantidade * compra.Acao.Valor)
                        {
                            Decimal valorTotalCompra = Convert.ToDecimal(compra.Quantidade * compra.Acao.Valor);
                            DeduzirSaldoCarteira(compra.Carteira.Id, valorTotalCompra);
                        }

                        //verificar se a acao está na lista de ativos da carteira
                        var ativoCarteira = compra?.Carteira?.Acoes?.FirstOrDefault(item => item.Acao.Id == compra.Acao.Id);
                        int? quantidade = compra?.Quantidade;

                        // TODO: codigo comentado apenas de teste para validar a inserção "InserirAtivoCarteira".
                        // if (ativoCarteira?.Id == 8) 
                        // {
                        //     DeletarAtivoCarteiraParaTeste(ativoCarteira.Id);
                        //     ativoCarteira = compra?.Carteira?.Acoes?.FirstOrDefault(item => item.Acao.Id == compra.Acao.Id);
                        // }

                        if (ativoCarteira != null)
                        {
                            AtualizarAtivoCarteira(ativoCarteira.Id, quantidade.HasValue ? quantidade.Value : 0);
                        }
                        else
                        {
                            int? idAcao = compra?.Acao?.Id;
                            int? idCarteira = compra?.Carteira?.Id;

                            InserirAtivoCarteira(idCarteira.HasValue ? idCarteira.Value : 0, idAcao.HasValue ? idAcao.Value : 0, quantidade.HasValue ? quantidade.Value : 0);
                        }

                        _logger.LogInformation("Processamento da Funcao Concluído com Sucesso.");

                    }
                    else
                        _logger.LogInformation("compra é nula na mensagem.");

                }
                else
                {
                    // Complete the message
                    // await messageActions.CompleteMessageAsync(message);

                    _logger.LogInformation("mensagem é nula.");
                }
            }
            catch (Exception ex)
            {
                //throw;
                _logger.LogWarning("Exception Message: {StackTraMessagece}", ex?.Message);
                _logger.LogWarning("Exception StackTrace: {StackTrace}", ex?.StackTrace?.ToString());
            }

        }

        private void DeletarAtivoCarteiraParaTeste(int idAtivoCarteira)
        {
            try
            {
                string sqlDeletar = "delete from Ativos where id = @idAtivoCarteira;";
                using (SqlConnection cnnDeletar = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmdUpdate = new SqlCommand(sqlDeletar, cnnDeletar))
                    {

                        cmdUpdate.Parameters.AddWithValue("@idAtivoCarteira", idAtivoCarteira);

                        cnnDeletar.Open();
                        cmdUpdate.ExecuteNonQuery();

                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private void AtualizarAtivoCarteira(int idAtivo, int quantidadeCompra)
        {
            // update Ativos set quantidade += @quantidade, dataCompra = getdate() where id = @id
            try
            {
                Int32 quantidadeExistente = 0;
                string sqlConsulta = "select Quantidade from Ativos where id = @idAtivo;";
                using (SqlConnection cnnConsulta = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmdConsulta = new SqlCommand(sqlConsulta, cnnConsulta))
                    {
                        cmdConsulta.Parameters.AddWithValue("@idAtivo", idAtivo);
                        cnnConsulta.Open();
                        using (var reader = cmdConsulta.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                quantidadeExistente = reader.GetInt32("quantidade");
                                reader.Close();
                            }
                            else 
                            {
                                _logger.LogInformation("Falha em AtualizarAtivoCarteira não encontrou o Ativo {idAtivo} para recuperar a Qtd e Somar a Qtd Comprada.", idAtivo);
                            }
                        }
                    }
                }
                var novaQuantidade = quantidadeExistente + quantidadeCompra;

                string sqlUpdate = "update Ativos set Quantidade = @novaQuantidade where id = @idAtivo;";
                using (SqlConnection cnnUpdate = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmdUpdate = new SqlCommand(sqlUpdate, cnnUpdate))
                    {

                        cmdUpdate.Parameters.AddWithValue("@idAtivo", idAtivo);
                        cmdUpdate.Parameters.AddWithValue("@novaQuantidade", novaQuantidade);

                        cnnUpdate.Open();
                        if(cmdUpdate.ExecuteNonQuery() > 0)
                            _logger.LogInformation("Sucesso em AtualizarAtivoCarteira para o Ativo {idAtivo} com a novaQuantidade {novaQuantidade} ", idAtivo, novaQuantidade);

                        else
                            _logger.LogInformation("Falha em AtualizarAtivoCarteira para o Ativo {idAtivo} com a novaQuantidade {novaQuantidade} ", idAtivo, novaQuantidade);

                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private void InserirAtivoCarteira(int idCarteira, int idAcao, int quantidade)
        {
            // insert into Ativos(idCateira, quantidade, dataCompra, acaoId) values(@idCarteira, @quantidade, getdate(), @acaoId)
            try
            {
                string sqlInsert = "insert into Ativos (IdCarteira, AcaoId, Quantidade, DataCompra) values (@idCarteira, @idAcao, @quantidade, @dataCompra);";
                using (SqlConnection cnnInsert = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmdInsert = new SqlCommand(sqlInsert, cnnInsert))
                    {

                        cmdInsert.Parameters.AddWithValue("@idCarteira", idCarteira);
                        cmdInsert.Parameters.AddWithValue("@idAcao", idAcao);
                        cmdInsert.Parameters.AddWithValue("@quantidade", quantidade);
                        cmdInsert.Parameters.AddWithValue("@dataCompra", DateTime.Now);

                        cnnInsert.Open();
                        if(cmdInsert.ExecuteNonQuery() > 0)
                            _logger.LogInformation("Sucesso em InserirAtivoCarteira para o Carteira {idCarteira} Ação {idAcao} Quantidade{quantidade} ", idCarteira, idAcao, quantidade);
                        else
                            _logger.LogInformation("Falha em InserirAtivoCarteira para o Carteira {idCarteira} Ação {idAcao} Quantidade{quantidade} ", idCarteira, idAcao, quantidade);
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private void DeduzirSaldoCarteira(int idCarteira, Decimal valorTotalCompra)
        {

            try
            {
                Decimal saldoExistente = 0.0M;
                string sqlConsulta = "select Saldo from Carteira where id = @idCarteira;";
                using (SqlConnection cnnConsulta = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmdConsulta = new SqlCommand(sqlConsulta, cnnConsulta))
                    {
                        cmdConsulta.Parameters.AddWithValue("@idCarteira", idCarteira);
                        cnnConsulta.Open();
                        using (var reader = cmdConsulta.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                saldoExistente = reader.GetDecimal("Saldo");
                                reader.Close();
                            }
                            else 
                            {
                                _logger.LogInformation("Falha em DeduzirSaldoCarteira não encontrou a Carteira {idCarteira} para recuperar o Saldo. ", idCarteira);
                            }
                        }
                    }

                }
                var novoSaldo = saldoExistente - valorTotalCompra;

                string sql = "update Carteira set Saldo = @novoSaldo where id = @idCarteira;";
                using (SqlConnection cnnUpdate = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmdUpdate = new SqlCommand(sql, cnnUpdate))
                    {

                        cmdUpdate.Parameters.AddWithValue("@idCarteira", idCarteira);
                        cmdUpdate.Parameters.AddWithValue("@novoSaldo", novoSaldo);

                        cnnUpdate.Open();
                        if(cmdUpdate.ExecuteNonQuery() > 0)
                            _logger.LogInformation("Sucesso em DeduzirSaldoCarteira idCarteira {idCarteira}  e novoSaldo {novoSaldo}. ", idCarteira, novoSaldo);
                        else
                            _logger.LogInformation("Falha em DeduzirSaldoCarteira idCarteira {idCarteira}  e novoSaldo {novoSaldo}. ", idCarteira, novoSaldo);

                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
