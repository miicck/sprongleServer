using System.Collections.Generic;
using System;
using System.Text;
using System.Net;
using System.Net.Sockets;

public static class server
{
    public const int PORT = 6900; // The port that the server listens on
    static UdpClient udp = new UdpClient(PORT); // The udp client listening on PORT
    static int currentEntityID = 1; // The last used entity id

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

    // Send an entity update to a client
    static void clientUpdate(int entityId, string[] updates, IPEndPoint client)
    {
        string toEncode = entityId + "";
        foreach (var u in updates)
            toEncode += "\n" + u;
        var data = ASCIIEncoding.ASCII.GetBytes(toEncode);
        udp.Send(data, data.Length, client);
    }

    // Server entrypoint
    public static void Main()
    {
        System.Console.WriteLine("Server started on port " + PORT);
        while (true)
        {
            // Receive a message from some client
            IPEndPoint client = new IPEndPoint(IPAddress.Any, 0);
            Byte[] receivedBytes = udp.Receive(ref client);
            string receivedText = ASCIIEncoding.ASCII.GetString(receivedBytes);
            System.Console.WriteLine("----------\n" + receivedText + "\n----------");

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
                clientUpdate(id, new string[] { "id:" + (currentEntityID++) }, client);
                return;
            }

            // Send the updates back to the client that sent them
            // as a test that the updates that the network entity
            // sent are actually compatible with its current state.
            // If this is not the case then this will result in a
            // feedback loop where a network entity continually
            // updates itself with its own state.
            clientUpdate(id, updates.ToArray(), client);
        }
    }
}