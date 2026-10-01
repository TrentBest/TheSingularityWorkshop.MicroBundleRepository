# Local MicroBundle Repository

A filesystem-backed development adapter for the same repository contracts used by durable storage.

It exists so the complete REST → repository → artifact flow can be exercised immediately without waiting for Azure provisioning.

Storage shape:

~~~text
{root}/
└── {bundleId}/
    └── {version}/
        └── {sha256}.bundle
~~~

It is intentionally not a different MicroBundle format and does not perform composition or dependency resolution.
