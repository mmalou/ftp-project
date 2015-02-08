using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading;
using System.Diagnostics;

namespace FtpServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            bool started = true;

            using (FtpServer server = new FtpServer(IPAddress.Any, 21))
            {
                server.Start();

                UserManager.LoadUser();

                Console.WriteLine("Serveur FTP démarré.\n");

                while (started)
                {
                    string command = "";

                    while (command == "")
                    {
                        Console.Write(">");
                        command = Console.ReadLine();
                    }

                    if (command == "help")
                    {
                        Console.WriteLine("Commandes disponibles");
                        Console.WriteLine("  Afficher les logs : log");
                        Console.WriteLine("  Lister les connections : list");
                        Console.WriteLine("  Ajouter un utilisateur : adduser -u LeNom -p Mdp");
                        Console.WriteLine("  Deconnecter un client : dispose -u NomClient");
                        Console.WriteLine("  Arrêter le serveur : quit");
                    }
                    else if (command == "log")
                    {
                        Process.Start("log.txt");
                    }
                    else if (command == "list")
                    {
                        if (server.ActiveConnections.Count == 0)
                            Console.WriteLine("Aucune connexion active actuellement.");
                        else
                        {
                            Console.WriteLine("Connexions actives :");
                            foreach (ClientConnection conn in server.ActiveConnections)
                            {
                                Console.WriteLine("  {0}", conn.ToString());
                            }
                        }
                    }
                    else if (command.Contains("adduser"))
                    {
                        string[] commandSplit = command.Split(' ');
                        if (commandSplit.Count() != 5 && commandSplit[0] == "adduser"
                            && commandSplit[1] != "-u" && commandSplit[3] != "-p")
                            Console.WriteLine("Synthaxe incorrecte, attendu : adduser -u LeNom -p Mdp");
                        else
                        {
                            if (UserManager.CheckExistingUser(commandSplit[2]))
                                Console.WriteLine("Nom d'utilisateur déjà existant.");
                            else
                            {
                                User userToAdd = new User
                                {
                                    Username = commandSplit[2],
                                    Password = commandSplit[4]
                                };
                                UserManager.AddUser(userToAdd);
                                Console.WriteLine("L'utilisateur {0} a bien été ajouté au serveur !", userToAdd.Username);
                            }
                        }
                    }
                    else if (command == "quit")
                    {
                        server.Dispose();
                        started = false;
                    }
                    else if (command.Contains("dispose"))
                    {
                        string[] commandSplit = command.Split(' ');
                        if (commandSplit[0] != "dispose" || commandSplit[1] != "-u")
                            Console.WriteLine("Synthaxe incorrecte, attendu : dispose -c NomClient");
                        else
                        {
                            string clientName = commandSplit[2].ToString();

                            List<ClientConnection> lstConn = server.ActiveConnections.Where(c => c.Username == clientName).ToList<ClientConnection>();

                            if (UserManager.Users.Where(u => u.Username == clientName).SingleOrDefault() == null)
                            {
                                Console.WriteLine("L'utilisateur {0} n'existe pas.", clientName);
                            }
                            else if (lstConn.Count == 0)
                                Console.WriteLine("L'utilisateur {0} n'est pas connecté.", clientName);
                            else
                            {
                                foreach (ClientConnection conn in lstConn)
                                {
                                    conn.Dispose();
                                    server.ActiveConnections.Remove(conn);
                                }
                                Console.WriteLine("Deconnection du client {0} du serveur.", clientName);
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Commande introuvable. Pour connaître les commandes disponibles veuillez taper \"help\"");
                    }
                }
            }
        }
    }
}
