# Custom Character Spawning

> [!NOTE]
> Full scripts for this page can be found in the SpawnCustomPlayer sample in the package manager or on [github]

Mirage comes with a CharacterSpawner which will automatically spawn a character object when a client connects.

Many games need character customization. You may want to pick the color of the hair, eyes, skin, height, race, etc.

In this case, you will need to create your own CharacterSpawner.  Follow these steps:

1) Create your player prefabs (as many as you need) and add them to the Spawnable Prefabs in your ClientObjectManager.
2) Create a message that describes your player. For example:
``` cs
public struct CreateMMOCharacterMessage
{
    public Race race;
    public string name;
    public Color hairColor;
    public Color eyeColor;
}

public enum Race
{
    Human,
    Elvish,
    Dwarvish,
}
```
3) Create Player Spawner class and add it to some GameObject in your scene
``` cs
public class CustomCharacterSpawner : MonoBehaviour
{
        [Header("References")]
        public NetworkClient Client;
        public NetworkServer Server;
        public ClientObjectManager ClientObjectManager;
        public ServerObjectManager ServerObjectManager;

        [Header("Prefabs")]
        // different prefabs based on the Race the player picks
        public CustomCharacter HumanPrefab;
        public CustomCharacter ElvishPrefab;
        public CustomCharacter DwarvishPrefab;
}
```
4) Drag the NetworkClient and NetworkServer and Scene manager to the fields
5) Hook into events:
```cs
public void Start()
{
    Client.Started.AddListener(OnClientStarted);
    Client.Authenticated.AddListener(OnClientAuthenticated);
    Server.Started.AddListener(OnServerStarted);
}
```
6) register the prefabs when the client starts
```cs
void OnClientStarted()
{
    // make sure all prefabs are Register so mirage can spawn the character for this client and for other players
    ClientObjectManager.RegisterPrefab(HumanPrefab.Identity);
    ClientObjectManager.RegisterPrefab(ElvishPrefab.Identity);
    ClientObjectManager.RegisterPrefab(DwarvishPrefab.Identity);
}
```
7) Send your message with your character data when your client connects, or after the user submits his preferences.
``` cs
// you can send the message here if you already know
// everything about the character at the time of player
// or at a later time when the user submits his preferences
void OnClientAuthenticated(INetworkPlayer player)
{
    var mmoCharacter = new CreateMMOCharacterMessage
    {
        // populate the message with your data
        name = "player user name",
        race = Race.Human,
        eyeColor = Color.red,
        hairColor = Color.black,
    };
    player.Send(mmoCharacter);
}
```
8) Receive your message in the server and spawn the player
```cs
void OnServerStarted()
{
    // wait for client to send us an AddPlayerMessage
    Server.MessageHandler.RegisterHandler<CreateMMOCharacterMessage>(OnCreateCharacter);
}

void OnCreateCharacter(INetworkPlayer player, CreateMMOCharacterMessage msg)
{
    CustomCharacter prefab = GetPrefab(msg);

    // create your character object
    // use the data in msg to configure it
    CustomCharacter character = Instantiate(prefab);

    // set syncVars before telling mirage to spawn character
    // this will cause them to be sent to client in the spawn message
    character.PlayerName = msg.name;
    character.hairColor = msg.hairColor;
    character.eyeColor = msg.eyeColor;

    // spawn it as the character object
    ServerObjectManager.AddCharacter(player, character.Identity);
}

CustomCharacter GetPrefab(CreateMMOCharacterMessage msg)
{
    // get prefab based on race
    CustomCharacter prefab;
    switch (msg.race)
    {
        case Race.Human: prefab = HumanPrefab; break;
        case Race.Elvish: prefab = ElvishPrefab; break;
        case Race.Dwarvish: prefab = DwarvishPrefab; break;
        // default case to check that client sent valid race.
        // the only reason it should be invalid is if the client's code was modified by an attacker
        // throw will cause the client to be kicked
        default: throw new InvalidEnumArgumentException("Invalid race given");
    }

    return prefab;
}
```

## Ready State

*This out of date and needs updating*

In addition to characters, player also have a “scene is ready” state. The server sends clients that are ready information about spawned game objects and state synchronization updates; clients which are not ready are not sent these updates. 

When a client initially connects to a server, their SceneIsReady property will be true. However initial spawning will not happen until a character has been added or you manually tell the server to send spawned objects.

Once a client has completed all its pre-game setup, and all its Assets are loaded, it can send a character message. As seen in the example above this will tell the server to spawn the player's character using `ServerObjectManager.AddCharacter`. After the character is spawned mirage will automatically send spawn message for the other spawned object to the client.

## Switching Characters

To replace the character game object for a player, use `ServerObjectManager.ReplaceCharacter`. This is useful having different game object for the player at different times, such as in game and a pregame lobby. The function takes the same arguments as `AddCharacter`, but allows there to already be a character for that player. The old character game object is not destroyed when ReplaceCharacter is called. The `NetworkRoomManager` uses this technique to switch from the `NetworkRoomPlayer` game object to a game play player game object when all the players in the room are ready.

You can also use `ReplaceCharacter` to respawn a player or change the object that represents the player. In some cases it is better to just disable a game object and reset its game attributes on respawn. The following code sample demonstrates how to actually replace the player game object with a new game object:

``` cs
public class CustomCharacterSpawner : MonoBehaviour
{
    public NetworkServer Server;
    public ServerObjectManager ServerObjectManager;

    public void Respawn(NetworkPlayer player, GameObject newPrefab)
    {
        // Cache a reference to the current character object
        GameObject oldPlayer = player.Identity.gameObject;

        var newCharacter = Instantiate(newPrefab);

        // Instantiate the new character object and broadcast to clients
        // NOTE: here we can use `keepAuthority: true` because we are calling Destroy on the old prefab immediately after.
        ServerObjectManager.ReplaceCharacter(player, newCharacter, keepAuthority: true);

        // Remove the previous character object that's now been replaced
        Server.Destroy(oldPlayer);
    }
}
```


## Destroying Characters

Once the character is finished with (eg game over, or player died) you can remove the character using `ServerObjectManager.DestroyCharacter`

```cs
public void OnPlayerDeath(INetworkPlayer player)
{
    ServerObjectManager.DestroyCharacter(player);
}
```

Alternatively you can use `ServerObjectManager.RemoveCharacter` to remove it as the player's character without destroying it
