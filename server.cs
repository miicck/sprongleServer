using System.Collections.Generic;
using System;
using System.Text;
using System.Net;
using System.Net.Sockets;

public static class server
{
    public const int PORT = 6900; // The port that the server listens on
    static UdpClient udp; // The udp client listening on PORT
    static int currentEntityID = 1; // The last used entity id
    static List<clientConnection> connectedClients = new List<clientConnection>(); // Online clients

    class clientConnection
    {
        public IPEndPoint address;
        public double lastActive;
    }

    // Send an entity update to the server
    public static void serverUpdate(this UdpClient c, int entityId, string[] updates)
    {
        string toEncode = entityId + "";
        if (updates != null)
            foreach (var u in updates)
                toEncode += "\n" + u;
        var data = ASCIIEncoding.ASCII.GetBytes(toEncode);
        c.Send(data, data.Length, "sprongle.com", PORT);
    }

    // Send an entity one shot message to the server
    public static void serverOneShot(this UdpClient c, int entityId, string message)
    {
        string toEncode = entityId + "\noneShot:" + message;
        var data = ASCIIEncoding.ASCII.GetBytes(toEncode);
        c.Send(data, data.Length, "sprongle.com", PORT);
    }

    // Send an entity update to a client
    static void clientUpdate(int entityId, string[] updates, IPEndPoint client)
    {
        string toEncode = entityId + "";
        foreach (var u in updates)
            toEncode += "\n" + u;
        var data = ASCIIEncoding.ASCII.GetBytes(toEncode);
        udp.Send(data, data.Length, client);
    }

    // Send an entity one shot message to the client
    static void clientOneShot(int entityId, string message, IPEndPoint client)
    {
        string toEncode = entityId + "";
        toEncode += "\noneShot:" + message;
        var data = ASCIIEncoding.ASCII.GetBytes(toEncode);
        udp.Send(data, data.Length, client);
    }

    static string lastMessage = "";

    // Server entrypoint
    public static void Main()
    {
        udp = new UdpClient(PORT); // The udp client listening on PORT
        System.Console.WriteLine("Server started on port " + PORT);
        while (true)
        {
            // Receive a message from some client
            IPEndPoint client = new IPEndPoint(IPAddress.Any, 0);
            Byte[] receivedBytes = udp.Receive(ref client);
            string receivedText = ASCIIEncoding.ASCII.GetString(receivedBytes);
            lastMessage = receivedText;

            // Keep track of clients
            bool inList = false;
            clientConnection connection = new clientConnection();
            connection.address = client;
            foreach (var c in connectedClients)
                if (sameAddress(c.address, client))
                {
                    inList = true;
                    connection = c;
                    break;
                }
            if (!inList)
            {
                // Not in list, create a new entry
                connectedClients.Add(connection);
            }
            connection.lastActive = time;

            /*
            // 10 second client timeout
            double currentTime = time;
            foreach (var c in connectedClients.ToArray())
                if (currentTime - c.lastActive > 10)
                {
                    System.Console.WriteLine("cliennt at " + c.address.Address + " timeout.");
                    connectedClients.Remove(c);
                }
            */

            // Logging
            System.Console.WriteLine("\n\nSERVER INFO");
            System.Console.WriteLine("Connected clients: " + connectedClients.Count);
            foreach (var c in connectedClients)
                System.Console.WriteLine("    Client, address: " + c.address.Address + ":" + c.address.Port + ", last active: " + c.lastActive);
            System.Console.WriteLine("\n-----Last server message-----");
            System.Console.WriteLine(lastMessage);
            System.Console.WriteLine("-----------------------------");

            // Parse the message
            var split = receivedText.Split('\n');
            if (split.Length < 1)
                throw new System.NotImplementedException("TODO: deal with this error case.");
            List<string> updates = new List<string>(split);
            updates.RemoveAt(0);
            int id = int.Parse(split[0]);

            // Assign this entity with a unique entity id
            if (id < 0)
            {
                clientUpdate(id, new string[] { "id:" + currentEntityID }, client);
                System.Console.WriteLine("Assigned new entity id: " + currentEntityID);
                ++currentEntityID;
                continue;
            }

            // Send one shot messages to all clients
            foreach (var u in updates)
                if (u.StartsWith("oneShot:"))
                {
                    var spl = u.Split(':');
                    if (spl.Length < 2)
                        throw new System.NotImplementedException("TODO: deal with this error case.");
                    foreach (var c in connectedClients)
                        clientOneShot(id, spl[1], c.address);
                    updates.Remove(u);
                }

            // Send the updates to all clients (including the one that
            // send the update to me, see below)

            // Send the updates back to the client that sent them
            // as a test that the updates that the network entity
            // sent are actually compatible with its current state.
            // If this is not the case then this will result in a
            // feedback loop where a network entity continually
            // updates itself with its own state.

            var updtArray = updates.ToArray();
            foreach (var c in connectedClients)
                clientUpdate(id, updtArray, c.address);
        }
    }

    public static bool sameAddress(IPEndPoint a, IPEndPoint b)
    {
        return a.Address.Equals(b.Address) && (a.Port == b.Port);
    }

    public static double time
    {
        get
        {
            return DateTime.Now.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
        }
    }
}