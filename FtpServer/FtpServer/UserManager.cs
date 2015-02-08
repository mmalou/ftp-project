using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Serialization;
using System.Xml;

namespace FtpServer
{
    public static class UserManager
    {
        #region fields & properties
        public static List<User> Users { get; private set; }

        private static XmlSerializer m_serializer;

        private static string m_rootUser;
        #endregion

        #region public methods
        public static void LoadUser()
        {
            m_rootUser = @"C:\Users\Maxime\Projets\FTP\";

            Users = new List<User>();

            m_serializer = new XmlSerializer(Users.GetType(), new XmlRootAttribute("Users"));

            if (File.Exists("users.xml"))
            {
                using (StreamReader r = new StreamReader("users.xml"))
                {
                    Users = m_serializer.Deserialize(r) as List<User>;
                }
            }
            else
            {
                Users.Add(new User {
                    Username = "maxime",
                    Password = "maxime",
                    HomeDir = @"C:\Users\Maxime\Projets\FTP\maxime"
                });

                using (StreamWriter w = new StreamWriter("users.xml"))
                {
                    m_serializer.Serialize(w, Users);
                }
            }
        }

        public static User Validate(string username, string password)
        {
            return (from u in Users where u.Username == username && u.Password == password select u).SingleOrDefault();
        }

        public static void AddUser(User userToAdd)
        {
            userToAdd.HomeDir = m_rootUser + userToAdd.Username;
            Users.Add(userToAdd);

            if (!Directory.Exists(userToAdd.HomeDir))
            {
                Directory.CreateDirectory(userToAdd.HomeDir);
                Directory.CreateDirectory(userToAdd.HomeDir + "/www");
            }

            using (StreamWriter w = new StreamWriter("users.xml"))
            {
                m_serializer.Serialize(w, Users);
            }
        }

        public static void RemoveUser(string username)
        {
            Users.Remove(Users.Where(u => u.Username == username).Single());

            XmlDocument doc = new XmlDocument();
            doc.Load("users.xml");
            XmlNode nodeToDelete = doc.SelectSingleNode("/Users/User[@username='" + username + "']");
            Console.WriteLine(nodeToDelete.Attributes[0].Value);
            nodeToDelete.ParentNode.RemoveChild(nodeToDelete);
            doc.Save("users.xml");
        }

        public static bool CheckExistingUser(string username)
        {
            if (Users.Where(u => u.Username == username).SingleOrDefault() == null)
                return false;
            return true;
        }
        #endregion
    }
}
