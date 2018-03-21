#define LOCAL_SERVER
using System.Collections.Generic;
using System;
using System.Text;
using System.Net;
using System.Net.Sockets;

public static class server
{
    public const int PORT = 6900; // The port that the server listens on
    public const float TIMEOUT = 10; // The timeout (in seconds) for clients to stop recciving updates
    const string aliveMessage = "STILL_ALIVE"; // Message sent to report a client is still alive
    static UdpClient udp; // The udp client listening on PORT
    static List<clientConnection> connectedClients = new List<clientConnection>(); // Online clients

    static List<List<string>> entityStates = new List<List<string>>();
    public static int firstAvailEntitySlot
    {
        get
        {
            for (int i = 0; i < entityStates.Count; ++i)
                if ((entityStates[i] == null) || (entityStates[i].Count == 0))
                    return i;
            return entityStates.Count;
        }
    }

    // Update a particular entity state
    static void updateState(int entity, int index, string val)
    {
        while (entityStates.Count <= entity)
            entityStates.Add(new List<string>());
        if (entityStates[entity] == null) entityStates[entity] = new List<string>();
        while (entityStates[entity].Count <= index)
            entityStates[entity].Add("");
        entityStates[entity][index] = val;
    }

    public static string address
    {
        get
        {
#if LOCAL_SERVER
            return getLocalIPAddress();
#else
            return "sprongle.com";
#endif
        }
    }

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
            {
                var s = u;
                toEncode += "\n" + s;
            }
        var data = ASCIIEncoding.ASCII.GetBytes(toEncode);
        c.Send(data, data.Length, address, PORT);
    }

    // Send an entity one shot message to the server
    public static void serverOneShot(this UdpClient c, int entityId, string message)
    {
        string toEncode = entityId + "\noneShot:" + message;
        var data = ASCIIEncoding.ASCII.GetBytes(toEncode);
        c.Send(data, data.Length, address, PORT);
    }

    // Tell the server this udp client is still alive
    public static void sendAliveMessage(this UdpClient c)
    {
        string toEncode = aliveMessage;
        var data = ASCIIEncoding.ASCII.GetBytes(toEncode);
        c.Send(data, data.Length, address, PORT);
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

    static string lastMessage = "";

#if LOCAL_SERVER
    public const int SIO_UDP_CONNRESET = -1744830452; // Magic number for ignoring a particular socket exeption when building the server locally
#endif

    // Server entrypoint
    public static void Main()
    {
        udp = new UdpClient(PORT); // The udp client listening on PORT

#if LOCAL_SERVER
        udp.Client.IOControl((IOControlCode)SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null); // Ignore a particular socket exception locally
#endif

        System.Console.WriteLine("Server started on port " + PORT);
        while (true)
        {
            // Receive a message from some client
            IPEndPoint client = new IPEndPoint(IPAddress.Any, 0);
            Byte[] receivedBytes = null;

            try
            {
                receivedBytes = udp.Receive(ref client);
            }
            catch (Exception e)
            {
                Console.WriteLine("UDP RECEIVE EXCEPTION");
                Console.WriteLine(e.ToString());
                continue;
            }

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
                // Not in list, create a new entry and send all entities
                connectedClients.Add(connection);
                for (int i = 0; i < entityStates.Count; ++i)
                    if ((entityStates[i] != null) && (entityStates[i].Count >0))
                        clientUpdate(i, entityStates[i].ToArray(), connection.address);
            }
            connection.lastActive = time;

            // 10 second client timeout
            double currentTime = time;
            foreach (var c in connectedClients.ToArray())
                if (currentTime - c.lastActive > 10)
                {
                    System.Console.WriteLine("cliennt at " + c.address.Address + " timeout.");
                    connectedClients.Remove(c);
                }

            // Logging
            System.Console.WriteLine("\n\nSERVER INFO");
            System.Console.WriteLine("Connected clients: " + connectedClients.Count);
            foreach (var c in connectedClients)
                System.Console.WriteLine("    Client, address: " + c.address.Address + ":" + c.address.Port + ", last active: " + c.lastActive);
            System.Console.WriteLine("\n-----Last server message, from: " + client.Address + ":" + client.Port + "-----");
            System.Console.WriteLine(lastMessage);
            System.Console.WriteLine("-----------------------------");
            System.Console.WriteLine("Entities:");
            for (int i = 0; i < entityStates.Count; ++i)
            {
                System.Console.WriteLine("  Entity: " + i);
                if ((entityStates[i] == null) || (entityStates[i].Count == 0))
                    Console.WriteLine("     free to reassign");
                else foreach (var s in entityStates[i])
                        System.Console.WriteLine("      " + s);
            }

            // Alive messages need not be parsed
            if (receivedText.StartsWith(aliveMessage))
                continue;

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
                int assignedID = firstAvailEntitySlot;
                clientUpdate(id, new string[] { "id:" + assignedID }, client);
                System.Console.WriteLine("Assigned new entity id: " + assignedID);
                continue;
            }

            // Keep network entites stored on the server up to date
            for (int i = 0; i < updates.Count; ++i)
            {
                var u = updates[i];
                if (u.StartsWith("destroy"))
                {
                    // Remove entity stored on server, queue destroy update to be sent
                    // and ignore all other updates
                    entityStates[id] = null;
                    updates = new List<string>() { "destroy" };
                    break;
                }

                var spl = u.Split(';');
                if (spl.Length >= 2)
                {
                    int index = int.Parse(spl[0]);
                    u = spl[1];
                    updateState(id, index, u);
                }
                updates[i] = u;
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

    static DateTime zeroTime;
    public static double time
    {
        get
        {
            if (zeroTime == default(DateTime))
                zeroTime = DateTime.Now;
            return DateTime.Now.Subtract(zeroTime).TotalSeconds;
        }
    }

    public static string getLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        throw new Exception("No network adapters with an IPv4 address in the system!");
    }
}