using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using UnityEngine;

[BepInPlugin("awl.beastsofburden.regression", "Beasts of Burden Cart Regression", "1.0.0")]
[BepInDependency("org.bepinex.plugins.beasts_of_burden")]
public sealed class CartRegressionProbe : BaseUnityPlugin
{
    private static readonly BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly MethodInfo UpdateCart = typeof(Vagon).GetMethod("Update", Any);
    private static readonly FieldInfo JointField = typeof(Vagon).GetField("m_attachJoin", Any);
    private int _passed;
    private int _failed;
    private readonly List<GameObject> _objects = new List<GameObject>();

    private IEnumerator Start()
    {
        Logger.LogInfo("BOB_REGRESSION_START");
        for (int i = 0; i < 240; ++i)
        {
            if (ZNetScene.instance != null && ZNet.instance != null && ZoneSystem.instance != null &&
                ZNetScene.instance.GetPrefab("Cart") != null &&
                ZNetScene.instance.GetPrefab("Wolf") != null &&
                ZNetScene.instance.GetPrefab("Lox") != null)
                break;
            yield return new WaitForSecondsRealtime(0.25f);
        }
        yield return new WaitForSecondsRealtime(2f);
        try { RunCases(); }
        catch (Exception ex) { Fail("UNHANDLED " + ex); }
        finally
        {
            foreach (GameObject obj in _objects)
            {
                if (obj == null) continue;
                if (ZNetScene.instance != null && obj.GetComponent<ZNetView>() != null)
                    ZNetScene.instance.Destroy(obj);
                else
                    Destroy(obj);
            }
            Logger.LogMessage(_failed == 0
                ? "BOB_REGRESSION_PASS total=" + _passed
                : "BOB_REGRESSION_FAIL passed=" + _passed + " failed=" + _failed);
        }
    }

    private GameObject Spawn(string name, Vector3 position)
    {
        GameObject prefab = ZNetScene.instance.GetPrefab(name);
        if (prefab == null) throw new InvalidOperationException("No prefab " + name);
        GameObject instance = Instantiate(prefab, position, Quaternion.identity);
        if (instance == null) throw new InvalidOperationException("Could not instantiate " + name);
        _objects.Add(instance);
        foreach (Rigidbody body in instance.GetComponentsInChildren<Rigidbody>())
            body.constraints = RigidbodyConstraints.FreezeAll;
        foreach (BaseAI ai in instance.GetComponents<BaseAI>())
            ai.enabled = false;
        return instance;
    }

    private static void Tick(Vagon cart)
    {
        if (UpdateCart == null) throw new MissingMethodException("Vagon.Update");
        UpdateCart.Invoke(cart, null);
    }

    private static Character Attached(Vagon cart)
    {
        ConfigurableJoint joint = (ConfigurableJoint)JointField.GetValue(cart);
        return joint != null && joint.connectedBody != null
            ? joint.connectedBody.gameObject.GetComponent<Character>() : null;
    }

    private static bool Near(Vector3 a, Vector3 b)
    {
        return Vector3.Distance(a, b) < 0.002f;
    }

    private static Vector3 AnimalOffset(Character animal)
    {
        return new Vector3(0f, 0.8f, -animal.GetRadius());
    }

    private void Check(bool condition, string name)
    {
        if (condition) { _passed++; Logger.LogInfo("BOB_PASS " + name); }
        else Fail(name);
    }

    private void Fail(string reason)
    {
        _failed++;
        Logger.LogError("BOB_FAIL " + reason);
    }

    private void RunCases()
    {
        Vector3 basePos = new Vector3(12000f, 500f, 12000f);
        Vagon cart = Spawn("Cart", basePos).GetComponent<Vagon>();
        Humanoid requester = Spawn("Draugr", basePos + Vector3.right * 4f).GetComponent<Humanoid>();
        Character wolf = Spawn("Wolf", basePos + Vector3.right * 50f).GetComponent<Character>();
        Character lox = Spawn("Lox", basePos + Vector3.right * 100f).GetComponent<Character>();
        wolf.SetTamed(true);
        lox.SetTamed(true);
        Vector3 originalOffset = cart.m_attachOffset;
        float originalDistance = cart.m_detachDistance;
        Check(cart.GetComponent<ZNetView>().IsOwner(), "new test cart owned by local server");
        Logger.LogInfo("BOB_CART_ORIGINAL offset=" + originalOffset + " detach=" + originalDistance);
        Logger.LogInfo("BOB_RADII wolf=" + wolf.GetRadius() + " lox=" + lox.GetRadius());
        string hover = cart.GetHoverText();
        Check(hover != null && hover.Contains("Pull cart yourself"), "hover advertises manual pulling");
        MethodInfo canAttach = typeof(Vagon).GetMethod("CanAttach", Any);
        wolf.transform.position = cart.m_attachPoint.position - AnimalOffset(wolf);
        Check((bool)canAttach.Invoke(cart, new object[] { wolf.gameObject }),
              "eligible wolf passes animal proximity check");
        Check(Near(cart.m_attachOffset, originalOffset) &&
              Math.Abs(cart.m_detachDistance - originalDistance) < 0.001f,
              "animal proximity checks do not mutate player cart settings");

        AnimalCycle(cart, requester, wolf, lox, originalOffset, originalDistance, "wolf");
        AnimalCycle(cart, requester, lox, wolf, originalOffset, originalDistance, "lox");
        AnimalCycle(cart, requester, wolf, lox, originalOffset, originalDistance, "wolf repeat");

        Vagon otherCart = Spawn("Cart", basePos + Vector3.right * 180f).GetComponent<Vagon>();
        otherCart.m_attachOffset = new Vector3(0f, 0.7f, -0.25f);
        otherCart.m_detachDistance = 2.65f;
        Vector3 independentOffset = otherCart.m_attachOffset;
        float independentDistance = otherCart.m_detachDistance;
        AnimalCycle(otherCart, requester, lox, wolf, independentOffset, independentDistance, "independent cart");
        Check(Near(cart.m_attachOffset, originalOffset) &&
              Math.Abs(cart.m_detachDistance - originalDistance) < 0.001f,
              "first cart unaffected by other cart's values");

        GameObject playerPrefab = ZNetScene.instance.GetPrefab("Player");
        if (playerPrefab == null)
        {
            Fail("Player prefab not registered on dedicated test server");
            return;
        }
        GameObject playerObject = Spawn("Player", cart.m_attachPoint.position - originalOffset);
        Player player = playerObject.GetComponent<Player>();
        Check(player != null && player.IsPlayer(), "player prefab available for native attachment");
        if (player == null) return;
        wolf.transform.position = basePos + Vector3.right * 100f;
        lox.transform.position = basePos + Vector3.right * 110f;
        playerObject.transform.position = cart.m_attachPoint.position - originalOffset;
        cart.Interact(player, false, false);
        Tick(cart);
        Check(Attached(cart) == player, "player attaches normally with animals away");
        Check(Near(cart.m_attachOffset, originalOffset) &&
              Math.Abs(cart.m_detachDistance - originalDistance) < 0.001f,
              "normal player uses original cart offset and distance");
        playerObject.transform.position = cart.m_attachPoint.position - originalOffset +
            Vector3.right * Math.Max(originalDistance + 1f, 2.6f);
        Tick(cart);
        Check(!cart.IsAttached(), "player detaches beyond original distance");
        Check(Near(cart.m_attachOffset, originalOffset), "player detachment preserves offset");

        wolf.transform.position = cart.m_attachPoint.position - AnimalOffset(wolf);
        playerObject.transform.position = cart.m_attachPoint.position - originalOffset;
        cart.Interact(player, false, true);
        Tick(cart);
        Check(Attached(cart) == player, "alternate use selects player despite nearby wolf");
        Check(Near(cart.m_attachOffset, originalOffset) &&
              Math.Abs(cart.m_detachDistance - originalDistance) < 0.001f,
              "alternate use keeps vanilla player geometry");
        cart.Interact(player, false, true);
        Tick(cart);
        Check(!cart.IsAttached(), "alternate use on occupied cart detaches");
        Check(Near(cart.m_attachOffset, originalOffset), "detaching from alternate use leaves original values");

        ZNetView view = cart.GetComponent<ZNetView>();
        long originalOwner = view.GetZDO().GetOwner();
        view.GetZDO().SetOwner(originalOwner + 99999L);
        Check(!view.IsOwner(), "remote-owner simulation active");
        playerObject.transform.position = cart.m_attachPoint.position - originalOffset;
        cart.Interact(player, false, true);
        Tick(cart);
        Check(!cart.IsAttached(), "unowned cart never attaches from requester-side prefix");
        view.GetZDO().SetOwner(originalOwner);
        Tick(cart);
        Check(Attached(cart) == player, "manual choice survives ownership transfer");
        cart.Interact(player, false, false);
        Tick(cart);
        Check(!cart.IsAttached(), "ownership handoff cart detaches normally");
        Logger.LogInfo("BOB_REGRESSION_GAME_BUILD server=" + Application.version);
    }

    private void AnimalCycle(
        Vagon cart, Humanoid requester, Character animal, Character distant,
        Vector3 originalOffset, float originalDistance, string label)
    {
        distant.transform.position = cart.m_attachPoint.position + Vector3.right * 150f;
        animal.transform.position = cart.m_attachPoint.position - AnimalOffset(animal);
        cart.Interact(requester, false, false);
        Tick(cart);
        float expectedDistance = animal.GetRadius() * 3.5f;
        Check(Attached(cart) == animal, label + " attaches on ordinary use");
        Check(Near(cart.m_attachOffset, AnimalOffset(animal)), label + " uses animal offset");
        Check(Math.Abs(cart.m_detachDistance - expectedDistance) < 0.01f,
              label + " uses radius-scaled detach distance");
        cart.Interact(requester, false, false);
        Tick(cart);
        Check(!cart.IsAttached(), label + " unhitched");
        Check(Near(cart.m_attachOffset, originalOffset) &&
              Math.Abs(cart.m_detachDistance - originalDistance) < 0.001f,
              label + " restores original values");
    }
}