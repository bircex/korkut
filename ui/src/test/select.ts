import { fireEvent, screen } from "@testing-library/react";

/** Opens a custom Select (the combobox trigger) and clicks the option with this name. */
export function choose(trigger: HTMLElement, option: string | RegExp) {
  fireEvent.click(trigger);
  fireEvent.click(screen.getByRole("option", { name: option }));
}
