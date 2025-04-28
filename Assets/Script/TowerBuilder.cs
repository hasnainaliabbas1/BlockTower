using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class TowerBuilder : MonoBehaviour
{
    [Header("Block Settings")]
    [SerializeField] private GameObject blockPrefab;
    [SerializeField] private float spawnOffset = 1f;
    [SerializeField] private float maxPlacementRadius = 2.0f;

    [Header("Physics Settings")]
    [SerializeField] private float swayForce = 0.2f;
    [SerializeField] private float collapseSlowMotionScale = 0.3f;
    [SerializeField] private float slowMotionDuration = 3f;

    [Header("UI Elements")]
    [SerializeField] private Button replayButton;
    [SerializeField] private TextMeshProUGUI scoreText;

    [Header("Camera Settings")]
    [SerializeField] private float cameraFollowSpeed = 2.0f;
    [SerializeField] private float cameraHeightOffset = 5.0f;

    private Camera mainCamera;
    private Transform towerRoot;
    private float towerHeight = 0f;
    private int score = 0;
    private bool isGameOver = false;
    private Vector3 initialRootPosition;
    private GameObject lastPlacedBlock;
    private Vector3 initialCameraPosition;

    void Awake()
    {
        if (replayButton == null)
            replayButton = GameObject.Find("ReplayButton")?.GetComponent<Button>();

        if (scoreText == null)
            scoreText = GameObject.Find("ScoreText")?.GetComponent<TextMeshProUGUI>();
    }

    void Start()
    {
        mainCamera = Camera.main;
        initialCameraPosition = mainCamera.transform.position;
        InitializeTowerRoot();

        if (replayButton != null)
        {
            replayButton.gameObject.SetActive(false);
            replayButton.onClick.AddListener(ResetGame);
        }
        else
        {
            Debug.LogError("Replay Button is not assigned!");
        }

        if (scoreText == null)
        {
            Debug.LogError("Score Text is not assigned!");
        }

        if (blockPrefab == null)
        {
            Debug.LogError("Block Prefab is not assigned!");
        }

        SpawnInitialBlock();
        UpdateScoreUI();
    }

    void Update()
    {
        if (isGameOver) return;

        HandleInputs();
        ApplyMinimalSway();
        CheckFallenBlocks();
        UpdateCameraPosition();
    }

    private void UpdateCameraPosition()
    {
        if (mainCamera != null)
        {
            float targetY = initialCameraPosition.y + towerHeight;
            if (targetY > initialCameraPosition.y)
            {
                Vector3 newPosition = new Vector3(
                    mainCamera.transform.position.x,
                    Mathf.Lerp(mainCamera.transform.position.y, targetY + cameraHeightOffset, Time.deltaTime * cameraFollowSpeed),
                    mainCamera.transform.position.z
                );

                mainCamera.transform.position = newPosition;
                mainCamera.transform.LookAt(new Vector3(0, towerHeight, 0));
            }
        }
    }

    private void InitializeTowerRoot()
    {
        towerRoot = new GameObject("TowerRoot").transform;
        initialRootPosition = towerRoot.position;

        GameObject basePlatform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        basePlatform.transform.localScale = new Vector3(5f, 0.2f, 5f);
        basePlatform.transform.position = Vector3.zero;
        basePlatform.tag = "Platform";

        Rigidbody baseRb = basePlatform.AddComponent<Rigidbody>();
        baseRb.isKinematic = true;
        baseRb.useGravity = false;
    }

    private void SpawnInitialBlock()
    {
        GameObject block = Instantiate(blockPrefab, new Vector3(0, 0.5f, 0), Quaternion.identity, towerRoot);
        block.tag = "Platform";
        block.transform.localScale = new Vector3(1.0f, 0.5f, 1.0f);

        Renderer renderer = block.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.5f, 1f);
        }

        Rigidbody rb = block.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = block.AddComponent<Rigidbody>();
        }

        rb.mass = 5f;
        rb.drag = 5f;
        rb.angularDrag = 10f;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        lastPlacedBlock = block;
        towerHeight = block.transform.position.y + 0.3f;
    }

    private void HandleInputs()
    {
        // Mouse input for non-touch devices
        if (Input.GetMouseButtonDown(0))
        {
            ProcessPointInput(Input.mousePosition);
        }

        // Touch input for mobile devices
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            // Only process touch when it begins
            if (touch.phase == TouchPhase.Began)
            {
                ProcessPointInput(touch.position);
            }
        }
    }

    private void ProcessPointInput(Vector3 screenPoint)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPoint);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider.CompareTag("Platform"))
            {
                Vector3 clickPoint = hit.point;
                bool isExistingBlock = hit.transform != towerRoot.GetChild(0);
                Vector3 spawnPosition;

                if (isExistingBlock)
                {
                    BoxCollider clickedCollider = hit.collider.GetComponent<BoxCollider>();
                    if (clickedCollider != null)
                    {
                        float blockHeight = clickedCollider.bounds.size.y;
                        spawnPosition = new Vector3(
                            hit.point.x,
                            hit.transform.position.y + blockHeight / 2 + 0.3f,
                            hit.point.z
                        );
                    }
                    else
                    {
                        spawnPosition = new Vector3(hit.point.x, hit.point.y + 0.5f, hit.point.z);
                    }
                }
                else
                {
                    spawnPosition = new Vector3(clickPoint.x, towerHeight, clickPoint.z);
                }

                Vector2 centerPos = Vector2.zero;
                Vector2 placementPos = new Vector2(spawnPosition.x, spawnPosition.z);

                if (Vector2.Distance(centerPos, placementPos) > maxPlacementRadius)
                {
                    Vector2 direction = (placementPos - centerPos).normalized;
                    placementPos = centerPos + direction * maxPlacementRadius;
                    spawnPosition = new Vector3(placementPos.x, spawnPosition.y, placementPos.y);
                }

                GameObject newBlock = SpawnBlock(spawnPosition);
                CheckTowerStabilityOnPlace(newBlock);
            }
        }
    }

    private GameObject SpawnBlock(Vector3 position)
    {
        GameObject block = Instantiate(blockPrefab, position, Quaternion.identity, towerRoot);
        block.tag = "Platform";

        float randomScale = Random.Range(0.8f, 1.0f);
        block.transform.localScale = new Vector3(randomScale, 0.5f, randomScale);

        Renderer renderer = block.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.5f, 1f);
        }

        Rigidbody rb = block.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = block.AddComponent<Rigidbody>();
        }

        rb.mass = 1.0f;
        rb.drag = 2.0f;
        rb.angularDrag = 10.0f;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        if (block.GetComponent<BoxCollider>() == null)
        {
            block.AddComponent<BoxCollider>();
        }

        // Add the TowerPhysics component to handle block-specific physics
        TowerPhysics blockPhysics = block.AddComponent<TowerPhysics>();
        blockPhysics.Initialize();

        lastPlacedBlock = block;
        towerHeight = Mathf.Max(towerHeight, position.y + 0.3f);
        score++;
        UpdateScoreUI();

        return block;
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + score.ToString();
        }
    }

    private void ApplyMinimalSway()
    {
        foreach (Transform block in towerRoot)
        {
            Rigidbody rb = block.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                float blockHeight = block.position.y;
                if (blockHeight > 10f)
                {
                    float heightFactor = (blockHeight - 10f) / 20f;
                    float sway = Mathf.Sin(Time.time * 0.5f) * swayForce * heightFactor;
                    rb.AddForce(new Vector3(sway, 0f, 0f), ForceMode.Acceleration);
                }
            }
        }
    }

    private void CheckTowerStabilityOnPlace(GameObject newBlock)
    {
        if (newBlock == null) return;
        StartCoroutine(DelayedStabilityCheck(newBlock));
    }

    private IEnumerator DelayedStabilityCheck(GameObject newBlock)
    {
        yield return new WaitForSeconds(0.1f);

        if (newBlock == null || isGameOver) yield break;

        Rigidbody rb = newBlock.GetComponent<Rigidbody>();
        if (rb == null) yield break;

        Vector3 blockPos = newBlock.transform.position;
        Vector3 blockScale = newBlock.transform.localScale;
        float blockWidth = Mathf.Max(blockScale.x, blockScale.z);

        bool hasSupport = false;

        Vector3[] checkPoints = new Vector3[]
        {
            blockPos,
            blockPos + new Vector3(blockWidth/3, 0, blockWidth/3),
            blockPos + new Vector3(-blockWidth/3, 0, blockWidth/3),
            blockPos + new Vector3(blockWidth/3, 0, -blockWidth/3),
            blockPos + new Vector3(-blockWidth/3, 0, -blockWidth/3)
        };

        foreach (Vector3 point in checkPoints)
        {
            if (Physics.Raycast(point, Vector3.down, out RaycastHit hit, 0.6f))
            {
                if (hit.collider.CompareTag("Platform") && hit.transform != newBlock.transform)
                {
                    hasSupport = true;
                    break;
                }
            }
        }

        float centerDistance = 0f;
        if (lastPlacedBlock != null && lastPlacedBlock != newBlock)
        {
            Vector2 lastBlockCenter = new Vector2(lastPlacedBlock.transform.position.x, lastPlacedBlock.transform.position.z);
            Vector2 thisBlockCenter = new Vector2(newBlock.transform.position.x, newBlock.transform.position.z);
            centerDistance = Vector2.Distance(lastBlockCenter, thisBlockCenter);
        }

        bool willCollapse = !hasSupport || centerDistance > 1.2f;

        if (willCollapse)
        {
            yield return new WaitForSeconds(0.2f);
            CollapseTower();
        }
    }

    private void CheckFallenBlocks()
    {
        foreach (Transform block in towerRoot)
        {
            if (block.position.y < -1f)
            {
                CollapseTower();
                break;
            }
        }
    }

    private void CollapseTower()
    {
        if (isGameOver) return;

        isGameOver = true;
        Debug.Log("Tower is collapsing!");

        foreach (Transform block in towerRoot)
        {
            Rigidbody rb = block.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.constraints = RigidbodyConstraints.None;
                rb.AddForce(new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)) * 2f, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse);
                rb.drag = 0.1f;
                rb.angularDrag = 0.1f;
            }
        }

        Time.timeScale = collapseSlowMotionScale;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        Invoke(nameof(ShowGameOverScreen), slowMotionDuration * collapseSlowMotionScale);
    }

    private void ShowGameOverScreen()
    {
        Debug.Log("Showing game over screen");

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        if (replayButton != null)
        {
            replayButton.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogError("Cannot show replay button as it is not assigned!");
        }
    }

    private void ResetGame()
    {
        Debug.Log("Game reset");

        StopAllCoroutines();
        CancelInvoke();

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        if (replayButton != null)
        {
            replayButton.gameObject.SetActive(false);
        }

        foreach (Transform child in towerRoot)
        {
            Destroy(child.gameObject);
        }

        Destroy(towerRoot.gameObject);
        towerHeight = 0f;
        score = 0;
        UpdateScoreUI();
        isGameOver = false;

        mainCamera.transform.position = initialCameraPosition;
        mainCamera.transform.rotation = Quaternion.identity;

        InitializeTowerRoot();
        SpawnInitialBlock();
    }
}