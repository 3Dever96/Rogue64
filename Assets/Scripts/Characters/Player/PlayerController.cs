using Animancer;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rogue.Characters.Player
{
    public class PlayerController : MonoBehaviour
    {
        // Component References
        public CharacterController Controller;

        // Inputs
        public Vector2 Move { get; private set; }
        public bool Jump { get; private set; }
        public bool Attack { get ; private set; }
        public bool Interact { get; private set; }

        // Input System Reference
        private PlayerInput input;

        private void Start()
        {
            Controller = GetComponent<CharacterController>();

            input = GetComponent<PlayerInput>();
        }

        private void OnEnable()
        {
            if (input == null)
            {
                input = GetComponent<PlayerInput>();
            }

            input.onActionTriggered += OnAction;
        }

        private void OnDisable()
        {
            input.onActionTriggered -= OnAction;
        }

        public void OnAction(InputAction.CallbackContext context)
        {

        }
    }
}
