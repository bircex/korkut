import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { SignInMethods } from "./Settings";

describe("SignInMethods", () => {
  it("shows e-mail and password for a password-only account", () => {
    render(<SignInMethods user={{ has_password: true, google_linked: false }} />);
    expect(screen.getByText("Giriş yöntemi")).toBeInTheDocument();
    expect(screen.getByText("E-posta ve parola")).toBeInTheDocument();
    expect(screen.queryByText("Google")).not.toBeInTheDocument();
  });

  it("shows Google for a Google-only account", () => {
    render(<SignInMethods user={{ has_password: false, google_linked: true }} />);
    expect(screen.getByText("Giriş yöntemi")).toBeInTheDocument();
    expect(screen.getByText("Google")).toBeInTheDocument();
    expect(screen.queryByText("E-posta ve parola")).not.toBeInTheDocument();
  });

  it("shows both when the account has a password and a linked Google account", () => {
    render(<SignInMethods user={{ has_password: true, google_linked: true }} />);
    expect(screen.getByText("Giriş yöntemleri")).toBeInTheDocument();
    expect(screen.getByText("E-posta ve parola")).toBeInTheDocument();
    expect(screen.getByText("Google")).toBeInTheDocument();
  });
});
